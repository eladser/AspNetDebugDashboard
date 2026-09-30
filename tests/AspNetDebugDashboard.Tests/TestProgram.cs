using AspNetDebugDashboard.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// No EnvironmentName here on purpose: WebApplicationFactory.WithWebHostBuilder(b => b.UseEnvironment(...))
// only has an effect if the entry point doesn't hardcode it first.
var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
};

var builder = WebApplication.CreateBuilder(options);

// Clear existing health check registrations to prevent duplicates
builder.Services.Configure<HealthCheckServiceOptions>(healthOptions =>
{
    healthOptions.Registrations.Clear();
});

// Add services
builder.Services.AddRouting();
builder.Services.AddControllers();

// Add Debug Dashboard for testing
builder.Services.AddDebugDashboard(dashboardOptions =>
{
    dashboardOptions.DatabasePath = $":memory:{Guid.NewGuid()}";
    dashboardOptions.IsEnabled = true;
    dashboardOptions.LogRequestBodies = true;
    dashboardOptions.LogResponseBodies = true;
    dashboardOptions.LogSqlQueries = true;
    dashboardOptions.LogExceptions = true;
    dashboardOptions.EnablePerformanceCounters = true;
    dashboardOptions.AllowDataExport = true;
    dashboardOptions.MaxEntries = 1000;
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.UseDebugDashboard();

app.MapGet("/test", async context =>
{
    await context.Response.WriteAsync("Test endpoint");
});

app.MapGet("/test-throw", async context =>
{
    await Task.CompletedTask;
    throw new InvalidOperationException("boom");
});

// Exercises outgoing HTTP capture: calls whatever url the test points it at with a plain
// `new HttpClient()`, optionally attaching a body/header that should get redacted.
app.MapGet("/test-outgoing", async context =>
{
    var url = context.Request.Query["url"].ToString();
    var withBody = context.Request.Query["withBody"] == "true";
    var withAuthHeader = context.Request.Query["withAuthHeader"] == "true";

    using var client = new HttpClient();
    if (int.TryParse(context.Request.Query["timeoutMs"], out var timeoutMs))
    {
        client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
    }
    if (withAuthHeader)
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer secret-token");
    }

    try
    {
        HttpResponseMessage response;
        if (withBody)
        {
            var content = new StringContent("{\"username\":\"bob\",\"password\":\"hunter2\"}", System.Text.Encoding.UTF8, "application/json");
            response = await client.PostAsync(url, content);
        }
        else
        {
            response = await client.GetAsync(url);
        }
        var body = await response.Content.ReadAsStringAsync();
        context.Response.StatusCode = (int)response.StatusCode;
        await context.Response.WriteAsync(body);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 502;
        await context.Response.WriteAsync(ex.Message);
    }
});

app.MapControllers();

app.Run();

// This is needed for WebApplicationFactory
public partial class Program { }
