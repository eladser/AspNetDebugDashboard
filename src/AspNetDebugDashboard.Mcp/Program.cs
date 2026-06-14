using AspNetDebugDashboard.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP servers talk JSON-RPC over stdout, so logs have to go to stderr or they
// corrupt the protocol stream.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// The dashboard's base URL. Defaults to the usual local address; override with
// the DEBUG_DASHBOARD_URL env var or a single command-line argument.
var baseUrl =
    (args.Length > 0 ? args[0] : null) ??
    Environment.GetEnvironmentVariable("DEBUG_DASHBOARD_URL") ??
    "http://localhost:5000";

// IHttpClientFactory manages the underlying handler lifetime.
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
    new DashboardClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), baseUrl));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
