using AspNetDebugDashboard.Mcp;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

// The MCP tools build query strings against the dashboard's REST API. A wrong
// param name silently returns the wrong data, so pin the URLs the tools call.
public class McpToolsTests
{
    private static (DashboardClient client, Func<string> lastUrl) FakeClient()
    {
        string? captured = null;
        var handler = new CapturingHandler(uri => captured = uri);
        var client = new DashboardClient(new HttpClient(handler), "http://localhost:5000");
        return (client, () => captured ?? "");
    }

    [Fact]
    public async Task RecentRequests_default_sorts_newest_first()
    {
        var (client, url) = FakeClient();
        await DebugTools.RecentRequests(client, count: 5);
        url().Should().Be("http://localhost:5000/_debug/api/requests?page=1&pageSize=5&sortBy=timestamp&sortDescending=true");
    }

    [Fact]
    public async Task RecentRequests_failedOnly_adds_filter()
    {
        var (client, url) = FakeClient();
        await DebugTools.RecentRequests(client, count: 20, failedOnly: true);
        url().Should().EndWith("&isSuccessful=false");
    }

    [Fact]
    public async Task RecentQueries_slowOnly_adds_filter()
    {
        var (client, url) = FakeClient();
        await DebugTools.RecentQueries(client, slowOnly: true);
        url().Should().Contain("/queries?").And.EndWith("&isSlowQuery=true");
    }

    [Fact]
    public async Task GetRequest_escapes_the_id()
    {
        var (client, url) = FakeClient();
        await DebugTools.GetRequest(client, "a/b c");
        url().Should().Be("http://localhost:5000/_debug/api/requests/a%2Fb%20c");
    }

    [Fact]
    public async Task Search_escapes_the_term()
    {
        var (client, url) = FakeClient();
        await DebugTools.Search(client, "order #1");
        url().Should().Be("http://localhost:5000/_debug/api/search?term=order%20%231");
    }

    private sealed class CapturingHandler(Action<string> onSend) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            onSend(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            });
        }
    }
}
