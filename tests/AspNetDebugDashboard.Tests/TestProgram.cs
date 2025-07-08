using AspNetDebugDashboard.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Clear existing health check registrations to prevent duplicates
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    options.Registrations.Clear();
});

// Add services
builder.Services.AddRouting();
builder.Services.AddControllers();

// Add Debug Dashboard for testing
builder.Services.AddDebugDashboard(options =>
{
    options.DatabasePath = $":memory:{Guid.NewGuid()}";
    options.IsEnabled = true;
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    options.LogSqlQueries = true;
    options.LogExceptions = true;
    options.EnablePerformanceCounters = true;
    options.AllowDataExport = true;
    options.MaxEntries = 1000;
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
