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

app.MapControllers();

app.Run();

// This is needed for WebApplicationFactory
public partial class Program { }
