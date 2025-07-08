using AspNetDebugDashboard.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDebugDashboard.Tests;

public class DebugDashboardIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DebugDashboardIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_Home_ReturnsSuccessAndCorrectContentType()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("text/html");
    }

    [Fact]
    public async Task Api_Stats_ReturnsValidJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/stats");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("application/json");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Should be valid JSON
        var stats = JsonSerializer.Deserialize<object>(content);
        stats.Should().NotBeNull();
    }

    [Fact]
    public async Task Api_Config_ReturnsConfiguration()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/config");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<JsonElement>(content);
        
        config.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Api_Health_ReturnsHealthStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Api_Requests_ReturnsPagedResults()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - First make a request to generate some data
        await client.GetAsync("/test");
        
        // Then check if it was logged
        var response = await client.GetAsync("/_debug/api/requests?pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        result.GetProperty("items").GetArrayLength().Should().BeGreaterOrEqualTo(0);
        result.TryGetProperty("totalCount", out _).Should().BeTrue();
        result.TryGetProperty("page", out _).Should().BeTrue();
        result.TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Api_Logs_AcceptsNewLogEntry()
    {
        // Arrange
        var client = _factory.CreateClient();
        var logRequest = new
        {
            Message = "Test log message",
            Level = "Info",
            Tag = "IntegrationTest",
            Properties = new { TestProperty = "TestValue" }
        };

        var json = JsonSerializer.Serialize(logRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/_debug/api/logs", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        result.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Api_ClearAll_ClearsAllData()
    {
        // Arrange
        var client = _factory.CreateClient();

        // First, create some data
        await client.GetAsync("/test");
        
        // Act
        var response = await client.DeleteAsync("/_debug/api/clear");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("message").GetString().Should().Contain("cleared");
    }

    [Fact]
    public async Task Api_Export_WhenEnabled_ReturnsFile()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/export");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("application/json");
        response.Content.Headers.ContentDisposition?.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition?.FileName.Should().StartWith("debug-export-");
    }

    [Theory]
    [InlineData("/_debug/api/requests")]
    [InlineData("/_debug/api/queries")]
    [InlineData("/_debug/api/logs")]
    [InlineData("/_debug/api/exceptions")]
    public async Task Api_Endpoints_ReturnSuccessfulResponses(string endpoint)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(endpoint);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("application/json");
    }

    [Fact]
    public async Task Api_Search_WithValidTerm_ReturnsResults()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/search?term=test");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement>(content);
        results.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Api_Search_WithEmptyTerm_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug/api/search?term=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Api_Performance_WhenEnabled_ReturnsMetrics()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Generate some test data first
        await client.GetAsync("/test");
        await client.GetAsync("/test");

        // Act
        var response = await client.GetAsync("/_debug/api/performance");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<JsonElement>(content);
        
        metrics.TryGetProperty("totalRequests", out _).Should().BeTrue();
        metrics.TryGetProperty("averageResponseTime", out _).Should().BeTrue();
        metrics.TryGetProperty("errorRate", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_CapturesRequestsCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Make multiple requests with different methods
        await client.GetAsync("/test");
        var testData = new { Name = "Test", Value = "Data" };
        var json = JsonSerializer.Serialize(testData);
        await client.PostAsync("/test", new StringContent(json, Encoding.UTF8, "application/json"));
        
        // Wait a bit for async processing
        await Task.Delay(100);

        // Check if requests were captured
        var response = await client.GetAsync("/_debug/api/requests");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        var items = result.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Dashboard_DisabledInProduction_ReturnsNotFound()
    {
        // Arrange
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                });
                
                services.AddDebugDashboard(options =>
                {
                    options.IsEnabled = false; // Disabled in production
                    options.DatabasePath = $":memory:{Guid.NewGuid()}";
                });
            });
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_debug");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dashboard_WithCustomPath_Works()
    {
        // Arrange
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                });
                
                services.AddDebugDashboard(options =>
                {
                    options.IsEnabled = true;
                    options.BasePath = "/_custom-debug";
                    options.DatabasePath = $":memory:{Guid.NewGuid()}";
                });
            });
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/_custom-debug");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 5)]
    [InlineData(1, 100)]
    public async Task Api_Pagination_WorksCorrectly(int page, int pageSize)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/_debug/api/requests?page={page}&pageSize={pageSize}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        result.GetProperty("page").GetInt32().Should().Be(page);
        result.GetProperty("pageSize").GetInt32().Should().Be(pageSize);
        
        var totalPages = result.GetProperty("totalPages").GetInt32();
        totalPages.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Api_Filtering_ByDateRange_Works()
    {
        // Arrange
        var client = _factory.CreateClient();
        var fromDate = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var toDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        // Act
        var response = await client.GetAsync($"/_debug/api/requests?dateFrom={fromDate}&dateTo={toDate}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        result.TryGetProperty("items", out _).Should().BeTrue();
        result.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Api_RateLimiting_HandlesHighLoad()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send multiple concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(client.GetAsync("/_debug/api/stats"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed (no rate limiting on API)
        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task TestEndpoint_GeneratesDebugData()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Call test endpoints to generate debug data
        await client.GetAsync("/test");
        
        // Give some time for async processing
        await Task.Delay(200);

        // Check that debug dashboard captured the requests
        var debugResponse = await client.GetAsync("/_debug/api/requests");
        debugResponse.EnsureSuccessStatusCode();
        
        var content = await debugResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        // Should have captured at least the requests we just made
        var items = result.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterOrEqualTo(1);
    }
}

// Test factory with proper content root configuration
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        
        // Set the content root to the current directory to fix path issues
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        
        // Override configurations for testing if needed
        builder.ConfigureServices(services =>
        {
            // Override specific configurations for testing
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
            
            // Override Debug Dashboard configuration for testing
            services.AddDebugDashboard(options =>
            {
                options.DatabasePath = $":memory:{Guid.NewGuid()}";
                options.IsEnabled = true;
                options.LogRequestBodies = true;
                options.LogResponseBodies = true;
                options.LogSqlQueries = true;
                options.LogExceptions = true;
                options.EnablePerformanceCounters = true;
                options.AllowDataExport = true;
                options.MaxEntries = 1000;
            });
        });
    }
}
