using AspNetDebugDashboard.Extensions;
using Microsoft.EntityFrameworkCore;
using SampleApp.Data;
using SampleApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
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

// Add Entity Framework - simplified without interceptor for now
builder.Services.AddDbContext<SampleDbContext>(options =>
{
    options.UseInMemoryDatabase("SampleDb");
});

// Add sample services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use Debug Dashboard middleware
app.UseDebugDashboard();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SeedData.SeedAsync(context);
}

app.Run();
