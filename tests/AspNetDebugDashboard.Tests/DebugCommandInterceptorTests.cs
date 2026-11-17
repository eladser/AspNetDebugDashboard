using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using AspNetDebugDashboard.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
    private static ServiceProvider BuildProvider(string environmentName, Action<DebugConfiguration>? configure = null, bool ef = false, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        services.AddDebugDashboard(o =>
        {
            o.DatabasePath = $":memory:{Guid.NewGuid()}";
            configure?.Invoke(o);
        });

        if (ef) services.AddDebugDashboardEntityFramework();

        var env = new TestWebHostEnvironment { EnvironmentName = environmentName };
        services.AddSingleton<IWebHostEnvironment>(env);
        extra?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static async Task RunQueryAsync(IServiceProvider provider)
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

    [Fact]
    public async Task ScopedInterceptor_LastQueryAfterScopeDisposed_StillStored()
    {
        var provider = BuildProvider("Development", ef: true);
        const int n = 30;

        for (var i = 0; i < n; i++) RunThenDisposeScope(provider);

        var storage = provider.GetRequiredService<IDebugStorage>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var inserts = 0;
        while (sw.ElapsedMilliseconds < 5000)
        {
            var all = await storage.GetSqlQueriesAsync(new DebugFilter { PageSize = 1000 });
            inserts = all.Items.Count(q => q.Query.Contains("INSERT INTO"));
            if (inserts >= n) break;
            await Task.Delay(25);
        }
        inserts.Should().Be(n);
    }

    [Fact]
    public async Task StorageThrowing_NeverReachesTheApp_AndIsLogged()
    {
        var logs = new CapturingLoggerProvider();
        var provider = BuildProvider("Development", ef: true, extra: s =>
        {
            var storage = new Mock<IDebugStorage>();
            storage.Setup(x => x.StoreSqlQueryAsync(It.IsAny<SqlQueryEntry>())).ThrowsAsync(new InvalidOperationException("disk full"));
            s.AddSingleton(storage.Object);
            s.AddLogging(b => b.AddProvider(logs));
        });

        RunThenDisposeScope(provider);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000 && !logs.Warnings.Any(w => w.Contains("Could not store SQL query") && w.Contains("disk full")))
            await Task.Delay(25);

        logs.Warnings.Should().Contain(w => w.Contains("Could not store SQL query") && w.Contains("disk full"));
    }

    // Parks the pool so the background store can't start until the request scope is gone,
    // which is what happens to the last query of a real request.
    private static void RunThenDisposeScope(IServiceProvider provider)
    {
        var gate = new ManualResetEventSlim();
        var started = new CountdownEvent(Environment.ProcessorCount);
        for (var i = 0; i < Environment.ProcessorCount; i++)
            ThreadPool.QueueUserWorkItem(_ => { started.Signal(); gate.Wait(); });
        started.Wait(TimeSpan.FromSeconds(5));

        try
        {
            var scope = provider.CreateScope();
            RunQueryAsync(scope.ServiceProvider).GetAwaiter().GetResult();
            scope.Dispose();
        }
        finally
        {
            gate.Set();
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(Warnings);
        public void Dispose() { }

        private sealed class Capture(System.Collections.Concurrent.ConcurrentQueue<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning) sink.Enqueue(formatter(state, exception) + " " + exception?.Message);
            }
        }
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
