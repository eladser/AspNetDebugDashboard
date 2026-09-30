using System.IO.Pipelines;
using System.Linq;
using AspNetDebugDashboard.Mcp;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AspNetDebugDashboard.Tests;

// Runs the real tool registration (WithToolsFromAssembly, same as Program.cs) over an
// in-memory pipe pair instead of real stdio, and drives it with the actual MCP client:
// initialize/discover + tools/list, same as an agent connecting over stdio would.
public class McpStdioSmokeTests
{
    private static readonly string[] ExpectedTools =
    [
        "get_stats", "recent_requests", "get_request", "recent_queries", "recent_exceptions",
        "recent_logs", "performance", "search",
        "list_feature_flags", "recent_jobs", "app_vitals", "recent_mail",
        "recent_dumps",
    ];

    [Fact]
    public async Task Tools_list_matches_expected_set_and_every_tool_has_an_input_schema()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new DashboardClient(new HttpClient(), "http://localhost:5000"));
        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(DebugTools).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));

        var tools = await client.ListToolsAsync();

        tools.Select(t => t.Name).Should().BeEquivalentTo(ExpectedTools);

        foreach (var tool in tools)
        {
            tool.JsonSchema.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
            tool.JsonSchema.GetProperty("type").GetString().Should().Be("object");
        }

        await host.StopAsync();
    }
}
