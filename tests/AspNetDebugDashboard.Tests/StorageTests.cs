using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Storage;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class StorageTests : IDisposable
{
    private readonly LiteDbStorage _storage;
    private readonly DebugConfiguration _config;

    public StorageTests()
    {
        _config = new DebugConfiguration
        {
            DatabasePath = ":memory:",
            MaxEntries = 1000
        };
        _storage = new LiteDbStorage(_config.DatabasePath, _config);
    }

    [Fact]
    public async Task StoreAndRetrieveRequest_ShouldWork()
    {
        // Arrange
        var request = new RequestEntry
        {
            Method = "GET",
            Path = "/api/test",
            StatusCode = 200,
            ExecutionTimeMs = 100
        };

        // Act
        var id = await _storage.StoreRequestAsync(request);
        var retrieved = await _storage.GetRequestAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(request.Method, retrieved.Method);
        Assert.Equal(request.Path, retrieved.Path);
        Assert.Equal(request.StatusCode, retrieved.StatusCode);
        Assert.Equal(request.ExecutionTimeMs, retrieved.ExecutionTimeMs);
    }

    [Fact]
    public async Task StoreAndRetrieveSqlQuery_ShouldWork()
    {
        // Arrange
        var query = new SqlQueryEntry
        {
            Query = "SELECT * FROM Products",
            ExecutionTimeMs = 50,
            Parameters = new Dictionary<string, object> { { "@id", 1 } }
        };

        // Act
        var id = await _storage.StoreSqlQueryAsync(query);
        var retrieved = await _storage.GetSqlQueryAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(query.Query, retrieved.Query);
        Assert.Equal(query.ExecutionTimeMs, retrieved.ExecutionTimeMs);
        Assert.Equal(query.Parameters.Count, retrieved.Parameters.Count);
    }

    [Fact]
    public async Task StoreAndRetrieveLog_ShouldWork()
    {
        // Arrange
        var log = new LogEntry
        {
            Level = "Info",
            Message = "Test log message",
            Tag = "Test",
            Properties = new Dictionary<string, object> { { "TestProp", "TestValue" } }
        };

        // Act
        var id = await _storage.StoreLogAsync(log);
        var retrieved = await _storage.GetLogAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(log.Level, retrieved.Level);
        Assert.Equal(log.Message, retrieved.Message);
        Assert.Equal(log.Tag, retrieved.Tag);
        Assert.Equal(log.Properties.Count, retrieved.Properties.Count);
    }

    [Fact]
    public async Task StoreAndRetrieveException_ShouldWork()
    {
        // Arrange
        var exception = new ExceptionEntry
        {
            Message = "Test exception",
            ExceptionType = "TestException",
            StackTrace = "at TestMethod() in TestFile.cs:line 1",
            Method = "GET",
            Path = "/api/test"
        };

        // Act
        var id = await _storage.StoreExceptionAsync(exception);
        var retrieved = await _storage.GetExceptionAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(exception.Message, retrieved.Message);
        Assert.Equal(exception.ExceptionType, retrieved.ExceptionType);
        Assert.Equal(exception.StackTrace, retrieved.StackTrace);
        Assert.Equal(exception.Method, retrieved.Method);
        Assert.Equal(exception.Path, retrieved.Path);
    }

    [Fact]
    public async Task GetRequests_WithFilter_ShouldWork()
    {
        // Arrange
        var request1 = new RequestEntry { Method = "GET", Path = "/api/test1", StatusCode = 200 };
        var request2 = new RequestEntry { Method = "POST", Path = "/api/test2", StatusCode = 404 };
        var request3 = new RequestEntry { Method = "GET", Path = "/api/test3", StatusCode = 200 };

        await _storage.StoreRequestAsync(request1);
        await _storage.StoreRequestAsync(request2);
        await _storage.StoreRequestAsync(request3);

        var filter = new DebugFilter
        {
            Method = "GET",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _storage.GetRequestsAsync(filter);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("GET", item.Method));
    }

    [Fact]
    public async Task GetStats_ShouldReturnCorrectStats()
    {
        // Arrange
        var request1 = new RequestEntry { Method = "GET", Path = "/api/test1", StatusCode = 200, ExecutionTimeMs = 100 };
        var request2 = new RequestEntry { Method = "POST", Path = "/api/test2", StatusCode = 404, ExecutionTimeMs = 200 };
        var query = new SqlQueryEntry { Query = "SELECT * FROM Products", ExecutionTimeMs = 50 };
        var log = new LogEntry { Level = "Info", Message = "Test log" };
        var exception = new ExceptionEntry { Message = "Test exception", ExceptionType = "TestException" };

        await _storage.StoreRequestAsync(request1);
        await _storage.StoreRequestAsync(request2);
        await _storage.StoreSqlQueryAsync(query);
        await _storage.StoreLogAsync(log);
        await _storage.StoreExceptionAsync(exception);

        // Act
        var stats = await _storage.GetStatsAsync();

        // Assert
        Assert.Equal(2, stats.TotalRequests);
        Assert.Equal(1, stats.TotalSqlQueries);
        Assert.Equal(1, stats.TotalLogs);
        Assert.Equal(1, stats.TotalExceptions);
        Assert.Equal(150, stats.AverageResponseTime); // (100 + 200) / 2
        Assert.Equal(50, stats.AverageSqlTime);
    }

    [Fact]
    public async Task CleanupAsync_ShouldRemoveOldEntries()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            var request = new RequestEntry
            {
                Method = "GET",
                Path = $"/api/test{i}",
                StatusCode = 200,
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            };
            await _storage.StoreRequestAsync(request);
        }

        // Act
        await _storage.CleanupAsync(5);

        // Get all requests
        var filter = new DebugFilter { Page = 1, PageSize = 100 };
        var result = await _storage.GetRequestsAsync(filter);

        // Assert
        Assert.Equal(5, result.TotalCount);
    }

    public void Dispose()
    {
        _storage?.Dispose();
    }
}

public class LiteDbStorageMaintenanceTests : IDisposable
{
    private readonly List<string> _tempPaths = new();

    private string NewTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"debugdash-{Guid.NewGuid():N}.db");
        _tempPaths.Add(path);
        return path;
    }

    private static string BigString(int size) => new string('x', size);

    [Fact]
    public async Task CleanupAsync_TrimsOutgoingHttpAndDumps()
    {
        var config = new DebugConfiguration { DatabasePath = ":memory:", MaxEntries = 3 };
        using var storage = new LiteDbStorage(config.DatabasePath, config);

        for (var i = 0; i < 10; i++)
        {
            await storage.StoreOutgoingHttpAsync(new OutgoingHttpEntry { Timestamp = DateTime.UtcNow.AddMinutes(-i) });
            await storage.StoreDumpAsync(new DumpEntry { Timestamp = DateTime.UtcNow.AddMinutes(-i) });
        }

        await storage.CleanupAsync(3);

        Assert.Equal(3, await storage.GetOutgoingHttpCountAsync());
        Assert.Equal(3, await storage.GetDumpCountAsync());
    }

    [Fact]
    public async Task GetDatabaseSizeAsync_MatchesMainFilePlusLogFile()
    {
        var dbPath = NewTempDbPath();
        var config = new DebugConfiguration { DatabasePath = dbPath };
        using var storage = new LiteDbStorage(dbPath, config);

        for (var i = 0; i < 50; i++)
        {
            await storage.StoreRequestAsync(new RequestEntry { RequestBody = BigString(2000) });
        }

        var logPath = Path.Combine(
            Path.GetDirectoryName(dbPath)!,
            Path.GetFileNameWithoutExtension(dbPath) + "-log" + Path.GetExtension(dbPath));

        var reportedSize = await storage.GetDatabaseSizeAsync();

        var expectedSize = new FileInfo(dbPath).Length + (File.Exists(logPath) ? new FileInfo(logPath).Length : 0);

        Assert.Equal(expectedSize, reportedSize);
        Assert.True(reportedSize > 0);
    }

    [Fact]
    public async Task CompactAsync_ShrinksOversizedFileDatabase()
    {
        var dbPath = NewTempDbPath();
        var config = new DebugConfiguration { DatabasePath = dbPath };
        using var storage = new LiteDbStorage(dbPath, config);

        for (var i = 0; i < 300; i++)
        {
            await storage.StoreRequestAsync(new RequestEntry
            {
                RequestBody = BigString(2000),
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        await storage.OptimizeAsync();
        var sizeBefore = await storage.GetDatabaseSizeAsync();

        await storage.DeleteOldestFractionAsync(0.9);
        await storage.CompactAsync();
        var sizeAfter = await storage.GetDatabaseSizeAsync();

        Assert.True(sizeAfter < sizeBefore, $"expected {sizeAfter} < {sizeBefore}");
        Assert.Equal(30, await storage.GetTotalEntriesAsync());
    }

    [Fact]
    public async Task CompactAsync_SkipsInMemoryDatabase()
    {
        var config = new DebugConfiguration { DatabasePath = ":memory:" };
        using var storage = new LiteDbStorage(config.DatabasePath, config);

        await storage.StoreRequestAsync(new RequestEntry());

        await storage.CompactAsync(); // should not throw

        Assert.Equal(1, await storage.GetTotalEntriesAsync());
    }

    [Fact]
    public async Task ConcurrentWrites_DuringCompact_LoseNoEntriesAndDbReopens()
    {
        var dbPath = NewTempDbPath();
        var config = new DebugConfiguration { DatabasePath = dbPath };

        const int writerCount = 20;

        using (var storage = new LiteDbStorage(dbPath, config))
        {
            for (var i = 0; i < 50; i++)
            {
                await storage.StoreRequestAsync(new RequestEntry { RequestBody = BigString(500) });
            }

            var writers = Enumerable.Range(0, writerCount)
                .Select(_ => storage.StoreRequestAsync(new RequestEntry { RequestBody = BigString(500) }));

            var compact = storage.CompactAsync();

            await Task.WhenAll(writers.Cast<Task>().Append(compact));

            Assert.Equal(50 + writerCount, await storage.GetTotalEntriesAsync());
        }

        using var reopened = new LiteDbStorage(dbPath, config);
        Assert.Equal(50 + writerCount, await reopened.GetTotalEntriesAsync());
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                var logPath = Path.Combine(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileNameWithoutExtension(path) + "-log" + Path.GetExtension(path));
                if (File.Exists(logPath)) File.Delete(logPath);
            }
            catch
            {
                // Windows file locks sometimes keep the handle open a moment after Dispose; not worth failing the run over.
            }
        }
    }
}
