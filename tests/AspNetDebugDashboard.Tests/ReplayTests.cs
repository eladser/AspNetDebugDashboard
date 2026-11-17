using System.Net;
using System.Text.Json;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class ReplayTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment("Development")
                .ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddDebugDashboard(o => o.DatabasePath = $":memory:{Guid.NewGuid()}");
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseDebugDashboard();
                    app.UseEndpoints(e =>
                    {
                        e.MapControllers();
                        e.MapGet("/x", () => "ok");
                        e.MapGet("/old", () => Results.Redirect("/new"));
                        e.MapGet("/new", () => "moved");
                    });
                }))
            .StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private async Task SendAsync(string path, string? replayOf)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (replayOf != null) req.Headers.TryAddWithoutValidation("X-Debug-Replay-Of", replayOf);
        using var res = await _client.SendAsync(req);
    }

    private async Task<IList<RequestEntry>> StoredAsync()
    {
        var storage = _host.Services.GetRequiredService<IDebugStorage>();
        return (await storage.GetRequestsAsync(new DebugFilter { PageSize = 100 })).Items;
    }

    [Fact]
    public async Task GuidHeader_IsStoredAsReplayOf_AndLeftOutOfHeaders()
    {
        var id = Guid.NewGuid().ToString();
        await SendAsync("/x", id);

        var entry = (await StoredAsync()).Single();
        entry.ReplayOf.Should().Be(id);
        entry.Headers.Keys.Should().NotContain(k => k.Equals("X-Debug-Replay-Of", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuidHeader_IsStoredInCanonicalForm()
    {
        var id = Guid.NewGuid();
        await SendAsync("/x", id.ToString("B").ToUpperInvariant());

        (await StoredAsync()).Single().ReplayOf.Should().Be(id.ToString());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public async Task NonGuidHeader_IsIgnored(string value)
    {
        await SendAsync("/x", value);

        var entry = (await StoredAsync()).Single();
        entry.ReplayOf.Should().BeNull();
        entry.Headers.Keys.Should().NotContain(k => k.Equals("X-Debug-Replay-Of", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NoHeader_LeavesReplayOfNull()
    {
        await SendAsync("/x", null);

        (await StoredAsync()).Single().ReplayOf.Should().BeNull();
    }

    [Fact]
    public async Task ReplayOfFilter_ReturnsOnlyReplaysOfThatId()
    {
        var a = Guid.NewGuid().ToString();
        var b = Guid.NewGuid().ToString();
        await SendAsync("/x", a);
        await SendAsync("/x?n=2", a);
        await SendAsync("/x", b);
        await SendAsync("/x", null);

        var res = await _client.GetAsync($"/_debug/api/requests?replayOf={a}");
        res.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var items = json.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.GetProperty("replayOf").GetString() == a);
        json.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ReplayOfRedirectingEndpoint_RecordsOneEntryWith302()
    {
        var id = Guid.NewGuid().ToString();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/old");
        req.Headers.Add("X-Debug-Replay-Of", id);
        using var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var stored = await StoredAsync();
        stored.Should().ContainSingle();
        stored[0].Path.Should().Be("/old");
        stored[0].StatusCode.Should().Be(302);
        stored[0].ReplayOf.Should().Be(id);
    }
}
