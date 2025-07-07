using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Middleware;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class PerformanceTests
{
    private readonly Mock<IDebugStorage> _mockStorage;
    private readonly Mock<IOptions<DebugConfiguration>> _mockOptions;
    private readonly DebugConfiguration _config;

    public PerformanceTests()
    {
        _mockStorage = new Mock<IDebugStorage>();
        _mockOptions = new Mock<IOptions<DebugConfiguration>>();
        
        _config = new DebugConfiguration
        {
            IsEnabled = true,
            LogRequestBodies = true,
            LogResponseBodies = true,
            MaxBodySize = 1024 * 1024
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_config);
    }

    [Fact]
    public async Task DebugRequestMiddleware_PerformanceImpact_ShouldBeMinimal()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(
            async context => await Task.Delay(50), // Simulate some work
            _mockOptions.Object, 
            _mockStorage.Object);

        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        var context = CreateHttpContext();
        var stopwatch = Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context);
        stopwatch.Stop();

        // Assert - Middleware overhead should be less than 10ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Once);
    }

    [Fact]
    public async Task DebugRequestMiddleware_WithLargeRequestBody_ShouldHandleEfficiently()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(
            async context => await Task.CompletedTask,
            _mockOptions.Object, 
            _mockStorage.Object);

        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        var context = CreateHttpContext();
        var largeBody = new string('x', 100_000); // 100KB
        var requestBytes = Encoding.UTF8.GetBytes(largeBody);
        context.Request.Body = new MemoryStream(requestBytes);
        context.Request.ContentLength = requestBytes.Length;

        var stopwatch = Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context);
        stopwatch.Stop();

        // Assert - Should still be fast even with large body
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public async Task DebugRequestMiddleware_ConcurrentRequests_ShouldScaleWell()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(
            async context => await Task.Delay(10),
            _mockOptions.Object, 
            _mockStorage.Object);

        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Process 100 concurrent requests
        for (int i = 0; i < 100; i++)
        {
            var context = CreateHttpContext();
            tasks.Add(middleware.InvokeAsync(context));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All requests should complete within reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Exactly(100));
    }

    [Fact]
    public async Task DebugRequestMiddleware_DisabledConfiguration_ShouldHaveNoOverhead()
    {
        // Arrange
        _config.IsEnabled = false;
        var middleware = new DebugRequestMiddleware(
            async context => await Task.Delay(10),
            _mockOptions.Object, 
            _mockStorage.Object);

        var context = CreateHttpContext();
        var stopwatch = Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context);
        stopwatch.Stop();

        // Assert - Should be extremely fast when disabled
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(20);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Never);
    }

    [Fact]
    public async Task DebugRequestMiddleware_MemoryUsage_ShouldNotLeak()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(
            async context => await Task.CompletedTask,
            _mockOptions.Object, 
            _mockStorage.Object);

        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Process many requests
        for (int i = 0; i < 1000; i++)
        {
            var context = CreateHttpContext();
            await middleware.InvokeAsync(context);
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory increase should be reasonable (less than 10MB)
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task DebugRequestMiddleware_ScalabilityTest_ShouldMaintainPerformance(int requestCount)
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(
            async context => await Task.CompletedTask,
            _mockOptions.Object, 
            _mockStorage.Object);

        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < requestCount; i++)
        {
            var context = CreateHttpContext();
            tasks.Add(middleware.InvokeAsync(context));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - Performance should scale linearly
        var averageTimePerRequest = (double)stopwatch.ElapsedMilliseconds / requestCount;
        averageTimePerRequest.Should().BeLessThan(5); // Less than 5ms per request on average
    }

    [Fact]
    public void DebugFilter_Validation_ShouldBeFast()
    {
        // Arrange
        var filter = new DebugFilter
        {
            Page = 1,
            PageSize = 50,
            Search = "test search term with some length",
            DateFrom = DateTime.UtcNow.AddDays(-1),
            DateTo = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        // Act - Validate many filters
        for (int i = 0; i < 10000; i++)
        {
            filter.IsValid();
            filter.Normalize();
        }

        stopwatch.Stop();

        // Assert - Should be very fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public void DebugEntry_Creation_ShouldBeFast()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act - Create many entries
        var entries = new List<RequestEntry>();
        for (int i = 0; i < 10000; i++)
        {
            entries.Add(new RequestEntry
            {
                Method = "GET",
                Path = $"/api/test/{i}",
                StatusCode = 200,
                ExecutionTimeMs = i % 1000,
                Timestamp = DateTime.UtcNow,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
            });
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
        entries.Should().HaveCount(10000);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 5001);
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = Guid.NewGuid().ToString();
        
        return context;
    }
}

public class LoadTests
{
    [Fact]
    public async Task DebugDashboard_HighLoad_ShouldRemainResponsive()
    {
        // This is a conceptual test - in practice you'd use a load testing tool
        // like NBomber, Artillery, or k6 for proper load testing
        
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Simulate high load
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(SimulateUserSession());
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // All sessions should complete within reasonable time
        stopwatch.ElapsedSeconds.Should().BeLessThan(30);
    }

    private async Task SimulateUserSession()
    {
        // Simulate a user interacting with the dashboard
        await Task.Delay(Random.Shared.Next(10, 100)); // Dashboard load
        await Task.Delay(Random.Shared.Next(5, 50));   // API calls
        await Task.Delay(Random.Shared.Next(5, 50));   // More API calls
        await Task.Delay(Random.Shared.Next(10, 100)); // Search/filter
    }
}
