using AspNetDebugDashboard.Extensions;
using AspNetMailbox;
using AspNetFlags;
using Microsoft.EntityFrameworkCore;
using SampleApp.Data;
using SampleApp.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Handle circular references in JSON serialization
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Debug Dashboard first
builder.Services.AddDebugDashboard(config =>
{
    config.IsEnabled = true;
    config.LogRequestBodies = true;
    config.LogResponseBodies = true;
    config.LogSqlQueries = true;
    config.LogExceptions = true;
    config.MaxEntries = 1000;
});

// SQLite so the EF interceptor has real SQL to capture
builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
{
    options.UseSqlite("Data Source=sample.db");
    options.AddDebugDashboard(sp);
});

// Mailbox: capture outbound email at /_mailbox (SMTP sink on :2525).
// AlwaysRunSink so the demo works even though the sample runs as Production.
builder.Services.AddMailbox(o => o.AlwaysRunSink = true);

// Feature flags at /_flags
builder.Services.AddFlags();

// Add sample services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// IMPORTANT: Force enable Debug Dashboard regardless of environment
app.UseDebugDashboard(forceEnable: true);
app.UseMailbox(forceEnable: true);
app.UseFlags(forceEnable: true);

// Seed a few flags so the demo has something to toggle (auto-discovered on first check).
using (var scope = app.Services.CreateScope())
{
    var ff = scope.ServiceProvider.GetRequiredService<IFeatureFlags>();
    foreach (var n in new[] { "new-checkout", "dark-mode", "beta-search", "promo-banner" }) ff.IsEnabled(n);
    ff.Set("dark-mode", true);
    ff.Set("beta-search", true);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Add routing and map controllers (including debug dashboard controllers)
app.UseRouting();
app.MapControllers();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SeedData.SeedAsync(context);
}

app.Run();

// Make the implicit Program class accessible to other assemblies
public partial class Program { }
