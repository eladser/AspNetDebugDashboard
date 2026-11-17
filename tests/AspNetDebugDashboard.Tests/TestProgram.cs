using AspNetDebugDashboard;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Interceptors;
using AspNetDebugDashboard.Tests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

// Timeline capture: each endpoint runs a fixed sequence of work against in-memory SQLite.
static (SqliteConnection, WidgetContext) NewDb(HttpContext ctx)
{
    var conn = new SqliteConnection("DataSource=:memory:");
    conn.Open();
    var opts = new DbContextOptionsBuilder<WidgetContext>()
        .UseSqlite(conn)
        .AddInterceptors(ctx.RequestServices.GetRequiredService<DebugCommandInterceptor>())
        .Options;
    return (conn, new WidgetContext(opts));
}

app.MapGet("/test-timeline", async context =>
{
    var (conn, db) = NewDb(context);
    using (conn)
    await using (db)
    {
        await db.Database.EnsureCreatedAsync();
        db.Widgets.Add(new Widget { Name = "a" });
        await db.SaveChangesAsync();
        await db.Widgets.ToListAsync();
    }

    using var client = new HttpClient();
    await client.GetAsync(context.Request.Query["url"].ToString());

    await context.RequestServices.GetRequiredService<IDebugLogger>().LogInfoAsync("timeline log");
    DebugDump.Dump(new { step = 4 }, "timeline dump");

    await context.Response.WriteAsync("ok");
});

app.MapGet("/test-timeline-sync", async context =>
{
    var (conn, db) = NewDb(context);
    using (conn)
    using (db)
    {
        db.Database.EnsureCreated();
        db.Widgets.Add(new Widget { Name = "a" });
        db.SaveChanges();
        db.Widgets.ToList();
        db.Widgets.ToList();
        db.Widgets.Count();
    }
    await context.Response.WriteAsync("ok");
});

app.MapGet("/test-timeline-slow", async context =>
{
    var (conn, db) = NewDb(context);
    using (conn)
    await using (db)
    {
        conn.CreateFunction("nap", (long ms) => { Thread.Sleep((int)ms); return 1L; });
        await db.Database.ExecuteSqlRawAsync("select 1");
        await db.Database.ExecuteSqlRawAsync("select nap(250)");
    }
    await context.Response.WriteAsync("ok");
});

app.MapGet("/test-timeline-fail", async context =>
{
    var (conn, db) = NewDb(context);
    using (conn)
    await using (db)
    {
        try { db.Database.ExecuteSqlRaw("select * from nope"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("select * from nope"); } catch { }
    }
    await context.Response.WriteAsync("ok");
});

app.MapGet("/test-timeline-parallel", async context =>
{
    var url = context.Request.Query["url"].ToString();
    using var client = new HttpClient();
    await Task.WhenAll(client.GetAsync(url), client.GetAsync(url));
    await context.Response.WriteAsync("ok");
});

app.MapControllers();

app.Run();

// This is needed for WebApplicationFactory
public partial class Program { }
