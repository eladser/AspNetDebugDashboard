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
using Microsoft.AspNetCore.Builder;
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
}

// Test WebApplicationFactory that creates a minimal test app
public class TestWebApplicationFactory : WebApplicationFactory<TestStartup>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        
        builder.ConfigureServices(services =>
        {
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
            
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

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        return base.CreateHost(builder);
    }
}

// Minimal test startup class
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseDebugDashboard();
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/test", async context =>
            {
                await context.Response.WriteAsync("Test endpoint");
            });
            
            endpoints.MapControllers();
        });
    }
}
