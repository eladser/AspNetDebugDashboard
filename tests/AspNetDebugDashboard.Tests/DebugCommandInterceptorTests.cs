using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using AspNetDebugDashboard.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class WidgetContext : DbContext
{
    public WidgetContext(DbContextOptions<WidgetContext> options) : base(options) { }
    public DbSet<Widget> Widgets => Set<Widget>();
}

public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DebugCommandInterceptorTests
{
    private static ServiceProvider BuildProvider(string environmentName, Action<DebugConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        services.AddDebugDashboard(o =>
        {
            o.DatabasePath = $":memory:{Guid.NewGuid()}";
            configure?.Invoke(o);
        });

        var env = new TestWebHostEnvironment { EnvironmentName = environmentName };
        services.AddSingleton<IWebHostEnvironment>(env);

        return services.BuildServiceProvider();
    }

    private static async Task RunQueryAsync(ServiceProvider provider)
    {
        var interceptor = provider.GetRequiredService<DebugCommandInterceptor>();
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        try
        {
            var options = new DbContextOptionsBuilder<WidgetContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;

            await using var context = new WidgetContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Widgets.Add(new Widget { Name = "test" });
            await context.SaveChangesAsync();
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public async Task Interceptor_InProduction_DoesNotRecordQueries()
    {
        var provider = BuildProvider("Production");

        await RunQueryAsync(provider);

        var storage = provider.GetRequiredService<IDebugStorage>();
        var queries = await storage.GetSqlQueriesAsync(new DebugFilter());
        queries.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Interceptor_InDevelopment_RecordsQueries()
    {
        var provider = BuildProvider("Development");

        await RunQueryAsync(provider);

        var storage = provider.GetRequiredService<IDebugStorage>();
        var queries = await storage.GetSqlQueriesAsync(new DebugFilter());
        queries.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Interceptor_ProductionWithAllowedEnvironments_RecordsQueries()
    {
        var provider = BuildProvider("Production", o => o.AllowedEnvironments = new() { "Production" });

        await RunQueryAsync(provider);

        var storage = provider.GetRequiredService<IDebugStorage>();
        var queries = await storage.GetSqlQueriesAsync(new DebugFilter());
        queries.Items.Should().NotBeEmpty();
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
