using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using AspNetDebugDashboard.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class ResponseBodyCaptureTests
{
    private static async Task<IHost> StartAsync(Action<IApplicationBuilder> pipeline)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s =>
                {
                    s.AddDebugDashboard(o =>
                    {
                        o.DatabasePath = $":memory:{Guid.NewGuid()}";
                        o.LogResponseBodies = true;
                    });
                })
                .Configure(app =>
                {
                    app.UseDeveloperExceptionPage();
                    app.UseMiddleware<DebugRequestMiddleware>();
                    pipeline(app);
                }))
            .StartAsync();
        return host;
    }

    private static async Task<RequestEntry> StoredAsync(IHost host)
    {
        var storage = host.Services.GetRequiredService<IDebugStorage>();
        var page = await storage.GetRequestsAsync(new DebugFilter { PageSize = 10 });
        return page.Items.Single();
    }

    [Fact]
    public async Task ThrowingEndpoint_StillGetsErrorPageAndRecords500()
    {
        using var host = await StartAsync(app => app.Run(_ => throw new InvalidOperationException("boom")));

        var res = await host.GetTestClient().GetAsync("/x");

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        (await res.Content.ReadAsStringAsync()).Should().NotBeEmpty();
        (await StoredAsync(host)).StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ResponseOverMaxBodySize_ArrivesIntact()
    {
        var size = 2 * 1024 * 1024;
        using var host = await StartAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = "application/octet-stream";
            await ctx.Response.Body.WriteAsync(new byte[size].Select((_, i) => (byte)(i % 251)).ToArray());
        }));

        var bytes = await host.GetTestClient().GetByteArrayAsync("/big");

        bytes.Length.Should().Be(size);
        bytes[1000].Should().Be((byte)(1000 % 251));
        (await StoredAsync(host)).ResponseBody.Should().StartWith("[Body too large");
    }

    [Fact]
    public async Task EventStream_IsNotBuffered()
    {
        var release = new TaskCompletionSource();
        using var host = await StartAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = "text/event-stream";
            await ctx.Response.WriteAsync("data: one\n\n");
            await ctx.Response.Body.FlushAsync();
            await release.Task;
        }));

        try
        {
            var get = host.GetTestClient().GetAsync("/events", HttpCompletionOption.ResponseHeadersRead);
            var done = await Task.WhenAny(get, Task.Delay(TimeSpan.FromSeconds(5)));
            done.Should().BeSameAs(get, "the first event has to reach the client before the endpoint finishes");

            using var res = await get;
            using var reader = new StreamReader(await res.Content.ReadAsStreamAsync());
            (await reader.ReadLineAsync()).Should().Be("data: one");
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
