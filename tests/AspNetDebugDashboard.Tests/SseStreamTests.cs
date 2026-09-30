using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using AspNetDebugDashboard.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class SseStreamTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SseStreamTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Stream_SendsEntryEvent_AfterRequestCaptured()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync("/_debug/api/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        // retry line confirms the server already subscribed before we generate the entry
        (await reader.ReadLineAsync(cts.Token)).Should().StartWith("retry:");
        await reader.ReadLineAsync(cts.Token);

        await client.GetAsync("/test", cts.Token);

        string? eventLine = null;
        string? dataLine = null;
        while (!cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;
            if (line.StartsWith("event: entry"))
            {
                eventLine = line;
                dataLine = await reader.ReadLineAsync(cts.Token);
                break;
            }
        }

        eventLine.Should().Be("event: entry");
        dataLine.Should().Contain("requests");
    }

    [Fact]
    public async Task Stream_OutsideAllowedEnvironments_ReturnsNotFound()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
                services.AddDebugDashboard(options =>
                {
                    options.IsEnabled = true;
                    options.DatabasePath = $":memory:{Guid.NewGuid()}";
                });
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/_debug/api/stream");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stream_WithAuthorizationFilterDenying_ReturnsUnauthorized()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
                services.AddDebugDashboard(options =>
                {
                    options.IsEnabled = true;
                    options.DatabasePath = $":memory:{Guid.NewGuid()}";
                    options.AuthorizationFilter = _ => false;
                });
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/_debug/api/stream");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stream_WhenRealTimeUpdatesDisabled_ReturnsNotFound()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
                services.AddDebugDashboard(options =>
                {
                    options.IsEnabled = true;
                    options.DatabasePath = $":memory:{Guid.NewGuid()}";
                    options.EnableRealTimeUpdates = false;
                });
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/_debug/api/stream");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // TestServer doesn't reliably propagate an HttpClient-side cancellation into
    // HttpContext.RequestAborted once the response headers are already flowing, so this
    // drives the controller action directly with a token we control - same code path the
    // real RequestAborted takes when a browser tab closes mid-stream.
    [Fact]
    public async Task Stream_Unsubscribes_WhenClientDisconnects()
    {
        var storage = new Mock<IDebugStorage>();
        var options = Options.Create(new DebugConfiguration { IsEnabled = true });
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(x => x.EnvironmentName).Returns("Development");
        var bus = new DebugEventBus();

        var controller = new DebugApiController(storage.Object, options, env.Object, bus)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Items = { [DebugDashboardAccess.BasePathMatchedItemKey] = true }
                }
            }
        };

        using var cts = new CancellationTokenSource();
        var streamTask = controller.Stream(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (bus.SubscriberCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        bus.SubscriberCount.Should().Be(1);

        cts.Cancel();
        await streamTask;

        bus.SubscriberCount.Should().Be(0);
    }
}
