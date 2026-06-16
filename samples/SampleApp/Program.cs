using AspNetDebugDashboard.Extensions;
using AspNetMailbox;
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

// Mailbox: capture outbound email at /_mailbox (SMTP sink on :2525)
builder.Services.AddMailbox();

// Add sample services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// IMPORTANT: Force enable Debug Dashboard regardless of environment
app.UseDebugDashboard(forceEnable: true);
app.UseMailbox(forceEnable: true);

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
