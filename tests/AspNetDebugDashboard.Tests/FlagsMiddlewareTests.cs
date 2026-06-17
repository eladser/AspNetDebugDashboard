using System.Text;
using AspNetDebugDashboard.Suite;
using AspNetFlags;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetDebugDashboard.Tests;

// Drives the real FlagsMiddleware end to end (routing, suite-nav injection, HTML + JSON serving)
// without spinning a web host.
public class FlagsMiddlewareTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"flagsmw-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _sp;
    private readonly FlagsMiddleware _mw;

    public FlagsMiddlewareTests()
    {
        var services = new ServiceCollection();
        services.AddFlags(o => o.DatabasePath = _db);               // registers Flags + its SuitePanel
        services.AddSuitePanel(new SuitePanel("Mailbox", "/_mailbox", "<svg/>", 10)); // a sibling
        _sp = services.BuildServiceProvider();
        _mw = new FlagsMiddleware(_ => Task.CompletedTask, _sp.GetRequiredService<FlagsOptions>());
    }

    private async Task<(int status, string body, string? contentType)> Call(string path, string method = "GET", string? jsonBody = null)
    {
        var ctx = new DefaultHttpContext { RequestServices = _sp };
        ctx.Request.Path = path;
        ctx.Request.Method = method;
        if (jsonBody != null) ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
        var stream = new MemoryStream();
        ctx.Response.Body = stream;
        await _mw.InvokeAsync(ctx, _sp.GetRequiredService<IFeatureFlags>());
        return (ctx.Response.StatusCode, Encoding.UTF8.GetString(stream.ToArray()), ctx.Response.ContentType);
    }

    [Fact]
    public async Task Page_serves_html_with_injected_suite_nav()
    {
        var (status, body, contentType) = await Call("/_flags");
        status.Should().Be(200);
        contentType.Should().Contain("text/html");
        body.Should().Contain("<!doctype html>", "the embedded page should be served");
        body.Should().NotContain("%SUITE_NAV%", "the nav token must be replaced");
        body.Should().Contain("\"current\":\"/_flags\"");
        body.Should().Contain("/_mailbox", "the sibling panel should be in the injected nav");
    }

    [Fact]
    public async Task Api_lists_flags_as_json()
    {
        _sp.GetRequiredService<IFeatureFlags>().Set("beta", true);
        var (status, body, contentType) = await Call("/_flags/api/flags");
        status.Should().Be(200);
        contentType.Should().Contain("application/json");
        body.Should().Contain("\"name\":\"beta\"").And.Contain("\"enabled\":true");
    }

    [Fact]
    public async Task Unknown_subpath_is_404()
    {
        var (status, _, _) = await Call("/_flags/api/nope");
        status.Should().Be(404);
    }

    [Fact]
    public async Task Oversized_flag_name_is_rejected()
    {
        var (status, _, _) = await Call("/_flags/api/flags/" + new string('a', 250), "POST", "{\"enabled\":true}");
        status.Should().Be(400);
    }

    public void Dispose()
    {
        _sp.Dispose();
        if (File.Exists(_db)) File.Delete(_db);
    }
}
