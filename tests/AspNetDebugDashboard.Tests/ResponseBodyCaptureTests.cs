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
    private static async Task<IHost> StartAsync(Action<IApplicationBuilder> pipeline, int? maxBodySize = null)
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
                        if (maxBodySize is int m) o.MaxBodySize = m;
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
    public async Task LargeResponse_ReachesTheClientWhileTheEndpointIsStillWriting()
    {
        var release = new TaskCompletionSource();
        var chunk = new byte[64 * 1024];
        Array.Fill(chunk, (byte)'a');
        using var host = await StartAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = "text/plain";
            for (var i = 0; i < 16; i++) await ctx.Response.Body.WriteAsync(chunk);
            await release.Task;
            for (var i = 0; i < 16; i++) await ctx.Response.Body.WriteAsync(chunk);
        }), maxBodySize: 64 * 1024);

        try
        {
            var get = host.GetTestClient().GetAsync("/dl", HttpCompletionOption.ResponseHeadersRead);
            var done = await Task.WhenAny(get, Task.Delay(TimeSpan.FromSeconds(5)));
            done.Should().BeSameAs(get, "bytes past MaxBodySize have to be passed through, not held back");

            using var res = await get;
            var stream = await res.Content.ReadAsStreamAsync();
            var first = new byte[1024];
            (await stream.ReadAsync(first)).Should().BeGreaterThan(0);

            release.SetResult();
            using var rest = new MemoryStream();
            await stream.CopyToAsync(rest);
            (rest.Length + first.Length).Should().BeGreaterThanOrEqualTo(2 * 1024 * 1024 - 1024);
        }
        finally
        {
            release.TrySetResult();
        }

        var req = await StoredAsync(host);
        req.ResponseBody.Should().StartWith("[Body too large");
        req.ResponseBody.Should().Contain((2 * 1024 * 1024).ToString());
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    public async Task BinaryResponseUnderMaxBodySize_IsStoredAsAPlaceholder(string type)
    {
        var payload = Enumerable.Range(0, 1000).Select(i => (byte)(0x80 + i % 100)).ToArray();
        using var host = await StartAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = type;
            await ctx.Response.Body.WriteAsync(payload);
        }));

        var bytes = await host.GetTestClient().GetByteArrayAsync("/bin");

        bytes.Should().Equal(payload);
        (await StoredAsync(host)).ResponseBody.Should().Be($"[Binary body: 1000 bytes, {type}]");
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    [InlineData("text/html; charset=utf-8")]
    public async Task TextLikeResponse_IsStoredAsText(string type)
    {
        using var host = await StartAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = type;
            await ctx.Response.WriteAsync("{\"a\":1}");
        }));

        await host.GetTestClient().GetAsync("/t");

        (await StoredAsync(host)).ResponseBody.Should().Be("{\"a\":1}");
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
