using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Middleware;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugRequestMiddlewareTests
{
    private readonly Mock<IDebugStorage> _mockStorage;
    private readonly Mock<IOptions<DebugConfiguration>> _mockOptions;
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<DebugRequestMiddleware>> _mockLogger;
    private readonly DebugConfiguration _config;

    public DebugRequestMiddlewareTests()
    {
        _mockStorage = new Mock<IDebugStorage>();
        _mockOptions = new Mock<IOptions<DebugConfiguration>>();
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<DebugRequestMiddleware>>();
        
        _config = new DebugConfiguration
        {
            IsEnabled = true,
            LogRequestBodies = true,
            LogResponseBodies = true,
            MaxBodySize = 1024 * 1024,
            ExcludedPaths = new[] { "/health" },
            ExcludedHeaders = new[] { "Authorization" }
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_config);
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_CallsNextWithoutLogging()
    {
        // Arrange
        _config.IsEnabled = false;
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithExcludedPath_CallsNextWithoutLogging()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        context.Request.Path = "/health";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithDebugPath_CallsNextWithoutLogging()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        context.Request.Path = "/_debug/api/stats";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithStaticFile_CallsNextWithoutLogging()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        context.Request.Path = "/css/style.css";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithValidRequest_LogsRequest()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test";
        context.Request.ContentType = "application/json";
        context.Response.StatusCode = 200;

        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            r.Method == "POST" && 
            r.Path == "/api/test" && 
            r.StatusCode == 200
        )), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithRequestBody_CapturesBody()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        
        var requestBody = "{\"test\": \"data\"}";
        var requestBytes = Encoding.UTF8.GetBytes(requestBody);
        context.Request.Body = new MemoryStream(requestBytes);
        context.Request.ContentLength = requestBytes.Length;
        context.Request.ContentType = "application/json";

        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            r.RequestBody == requestBody
        )), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithLargeRequestBody_TruncatesBody()
    {
        // Arrange
        _config.MaxBodySize = 10; // Very small limit
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        
        var largeBody = new string('x', 1000);
        var requestBytes = Encoding.UTF8.GetBytes(largeBody);
        context.Request.Body = new MemoryStream(requestBytes);
        context.Request.ContentLength = requestBytes.Length;

        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            r.RequestBody != null && r.RequestBody.Contains("[Body too large:")
        )), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithException_LogsException()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        
        var expectedException = new InvalidOperationException("Test exception");
        _mockNext.Setup(x => x(context)).ThrowsAsync(expectedException);
        
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");
        _mockStorage.Setup(x => x.StoreExceptionAsync(It.IsAny<ExceptionEntry>()))
                   .ReturnsAsync("exception-id");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            r.Exception != null
        )), Times.Once);
        
        _mockStorage.Verify(x => x.StoreExceptionAsync(It.Is<ExceptionEntry>(e => 
            e.Message == "Test exception" && 
            e.ExceptionType == "InvalidOperationException"
        )), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithExcludedHeaders_FiltersHeaders()
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        
        context.Request.Headers.Add("Authorization", "Bearer token");
        context.Request.Headers.Add("Content-Type", "application/json");
        context.Request.Headers.Add("User-Agent", "Test Agent");

        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            !r.Headers.ContainsKey("Authorization") &&
            r.Headers.ContainsKey("Content-Type") &&
            r.Headers.ContainsKey("User-Agent")
        )), Times.Once);
    }

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData(null, "Unknown")]
    public async Task InvokeAsync_CapturesClientIpAddress(string? remoteIp, string expectedIp)
    {
        // Arrange
        var middleware = new DebugRequestMiddleware(_mockNext.Object, _mockOptions.Object, _mockStorage.Object);
        var context = CreateHttpContext();
        
        if (remoteIp != null)
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        }

        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);
        _mockStorage.Setup(x => x.StoreRequestAsync(It.IsAny<RequestEntry>()))
                   .ReturnsAsync("test-id");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockStorage.Verify(x => x.StoreRequestAsync(It.Is<RequestEntry>(r => 
            r.IPAddress == expectedIp
        )), Times.Once);
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
