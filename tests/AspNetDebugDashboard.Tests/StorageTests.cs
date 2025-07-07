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
