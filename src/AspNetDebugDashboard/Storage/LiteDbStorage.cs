using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using LiteDB;
using System.Text.Json;

namespace AspNetDebugDashboard.Storage;

public class LiteDbStorage : IDebugStorage
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<RequestEntry> _requests;
    private readonly ILiteCollection<SqlQueryEntry> _sqlQueries;
    private readonly ILiteCollection<LogEntry> _logs;
    private readonly ILiteCollection<ExceptionEntry> _exceptions;
    private readonly DebugConfiguration _config;

    public LiteDbStorage(string connectionString, DebugConfiguration config)
    {
        _config = config;
        _database = new LiteDatabase(connectionString);
        
        _requests = _database.GetCollection<RequestEntry>("requests");
        _sqlQueries = _database.GetCollection<SqlQueryEntry>("sqlQueries");
        _logs = _database.GetCollection<LogEntry>("logs");
        _exceptions = _database.GetCollection<ExceptionEntry>("exceptions");
        
        // Create indexes for better performance
        _requests.EnsureIndex(x => x.Timestamp);
        _requests.EnsureIndex(x => x.StatusCode);
        _requests.EnsureIndex(x => x.Method);
        _requests.EnsureIndex(x => x.Path);
        
        _sqlQueries.EnsureIndex(x => x.Timestamp);
        _sqlQueries.EnsureIndex(x => x.RequestId);
        _sqlQueries.EnsureIndex(x => x.ExecutionTimeMs);
        
        _logs.EnsureIndex(x => x.Timestamp);
        _logs.EnsureIndex(x => x.Level);
        _logs.EnsureIndex(x => x.RequestId);
        _logs.EnsureIndex(x => x.Tag);
        
        _exceptions.EnsureIndex(x => x.Timestamp);
        _exceptions.EnsureIndex(x => x.RequestId);
        _exceptions.EnsureIndex(x => x.ExceptionType);
    }

    public async Task<string> StoreRequestAsync(RequestEntry request)
    {
        await Task.Run(() => _requests.Insert(request));
        return request.Id;
    }

    public async Task<string> StoreSqlQueryAsync(SqlQueryEntry query)
    {
        await Task.Run(() => _sqlQueries.Insert(query));
        return query.Id;
    }

    public async Task<string> StoreLogAsync(LogEntry log)
    {
        await Task.Run(() => _logs.Insert(log));
        return log.Id;
    }

    public async Task<string> StoreExceptionAsync(ExceptionEntry exception)
    {
        await Task.Run(() => _exceptions.Insert(exception));
        return exception.Id;
    }

    public async Task<PagedResult<RequestEntry>> GetRequestsAsync(DebugFilter filter)
    {
        return await Task.Run(() =>
        {
            var query = _requests.Query();
            
            ApplyCommonFilters(query, filter);
            
            if (!string.IsNullOrEmpty(filter.Method))
                query = query.Where(x => x.Method == filter.Method);
            
            if (!string.IsNullOrEmpty(filter.Path))
                query = query.Where(x => x.Path.Contains(filter.Path));
            
            if (filter.StatusCode.HasValue)
                query = query.Where(x => x.StatusCode == filter.StatusCode.Value);
            
            var totalCount = query.Count();
            
            var items = query
                .OrderBy(GetSortExpression<RequestEntry>(filter.SortBy), filter.SortDescending ? Query.Descending : Query.Ascending)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToList();

            return new PagedResult<RequestEntry>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        });
    }

    public async Task<PagedResult<SqlQueryEntry>> GetSqlQueriesAsync(DebugFilter filter)
    {
        return await Task.Run(() =>
        {
            var query = _sqlQueries.Query();
            
            ApplyCommonFilters(query, filter);
            
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(x => x.Query.Contains(filter.Search));
            
            var totalCount = query.Count();
            
            var items = query
                .OrderBy(GetSortExpression<SqlQueryEntry>(filter.SortBy), filter.SortDescending ? Query.Descending : Query.Ascending)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToList();

            return new PagedResult<SqlQueryEntry>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        });
    }

    public async Task<PagedResult<LogEntry>> GetLogsAsync(DebugFilter filter)
    {
        return await Task.Run(() =>
        {
            var query = _logs.Query();
            
            ApplyCommonFilters(query, filter);
            
            if (!string.IsNullOrEmpty(filter.Level))
                query = query.Where(x => x.Level == filter.Level);
            
            if (!string.IsNullOrEmpty(filter.Tag))
                query = query.Where(x => x.Tag == filter.Tag);
            
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(x => x.Message.Contains(filter.Search));
            
            var totalCount = query.Count();
            
            var items = query
                .OrderBy(GetSortExpression<LogEntry>(filter.SortBy), filter.SortDescending ? Query.Descending : Query.Ascending)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToList();

            return new PagedResult<LogEntry>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        });
    }

    public async Task<PagedResult<ExceptionEntry>> GetExceptionsAsync(DebugFilter filter)
    {
        return await Task.Run(() =>
        {
            var query = _exceptions.Query();
            
            ApplyCommonFilters(query, filter);
            
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(x => x.Message.Contains(filter.Search) || x.ExceptionType.Contains(filter.Search));
            
            var totalCount = query.Count();
            
            var items = query
                .OrderBy(GetSortExpression<ExceptionEntry>(filter.SortBy), filter.SortDescending ? Query.Descending : Query.Ascending)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToList();

            return new PagedResult<ExceptionEntry>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        });
    }

    public async Task<RequestEntry?> GetRequestAsync(string id)
    {
        return await Task.Run(() => _requests.FindById(id));
    }

    public async Task<SqlQueryEntry?> GetSqlQueryAsync(string id)
    {
        return await Task.Run(() => _sqlQueries.FindById(id));
    }

    public async Task<LogEntry?> GetLogAsync(string id)
    {
        return await Task.Run(() => _logs.FindById(id));
    }

    public async Task<ExceptionEntry?> GetExceptionAsync(string id)
    {
        return await Task.Run(() => _exceptions.FindById(id));
    }

    public async Task<DebugStats> GetStatsAsync()
    {
        return await Task.Run(() =>
        {
            var totalRequests = _requests.Count();
            var totalSqlQueries = _sqlQueries.Count();
            var totalExceptions = _exceptions.Count();
            var totalLogs = _logs.Count();
            
            var avgResponseTime = _requests.Query()
                .Select(x => x.ExecutionTimeMs)
                .ToList()
                .DefaultIfEmpty(0)
                .Average();
            
            var avgSqlTime = _sqlQueries.Query()
                .Select(x => x.ExecutionTimeMs)
                .ToList()
                .DefaultIfEmpty(0)
                .Average();
            
            var statusCodes = _requests.Query()
                .GroupBy(x => x.StatusCode)
                .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                .ToDictionary(x => x.StatusCode, x => x.Count);
            
            var methods = _requests.Query()
                .GroupBy(x => x.Method)
                .Select(g => new { Method = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Method, x => x.Count);
            
            var exceptionTypes = _exceptions.Query()
                .GroupBy(x => x.ExceptionType ?? "Unknown")
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Type, x => x.Count);
            
            var slowestRequests = _requests.Query()
                .OrderBy(x => x.ExecutionTimeMs, Query.Descending)
                .Limit(10)
                .Select(x => new SlowRequest
                {
                    Id = x.Id,
                    Method = x.Method,
                    Path = x.Path,
                    ExecutionTimeMs = x.ExecutionTimeMs,
                    Timestamp = x.Timestamp
                })
                .ToList();
            
            var slowestQueries = _sqlQueries.Query()
                .OrderBy(x => x.ExecutionTimeMs, Query.Descending)
                .Limit(10)
                .Select(x => new SlowQuery
                {
                    Id = x.Id,
                    Query = x.Query.Length > 100 ? x.Query.Substring(0, 100) + "..." : x.Query,
                    ExecutionTimeMs = x.ExecutionTimeMs,
                    Timestamp = x.Timestamp
                })
                .ToList();
            
            return new DebugStats
            {
                TotalRequests = totalRequests,
                TotalSqlQueries = totalSqlQueries,
                TotalExceptions = totalExceptions,
                TotalLogs = totalLogs,
                AverageResponseTime = avgResponseTime,
                AverageSqlTime = avgSqlTime,
                StatusCodeDistribution = statusCodes,
                RequestMethodDistribution = methods,
                ExceptionTypeDistribution = exceptionTypes,
                SlowestRequests = slowestRequests,
                SlowestQueries = slowestQueries
            };
        });
    }

    public async Task CleanupAsync(int maxEntries)
    {
        await Task.Run(() =>
        {
            CleanupCollection(_requests, maxEntries);
            CleanupCollection(_sqlQueries, maxEntries);
            CleanupCollection(_logs, maxEntries);
            CleanupCollection(_exceptions, maxEntries);
        });
    }

    public async Task ClearAllAsync()
    {
        await Task.Run(() =>
        {
            _requests.DeleteAll();
            _sqlQueries.DeleteAll();
            _logs.DeleteAll();
            _exceptions.DeleteAll();
        });
    }

    private void ApplyCommonFilters<T>(ILiteQueryable<T> query, DebugFilter filter) where T : DebugEntry
    {
        if (filter.FromDate.HasValue)
            query = query.Where(x => x.Timestamp >= filter.FromDate.Value);
        
        if (filter.ToDate.HasValue)
            query = query.Where(x => x.Timestamp <= filter.ToDate.Value);
    }

    private string GetSortExpression<T>(string sortBy) where T : DebugEntry
    {
        return sortBy.ToLower() switch
        {
            "timestamp" => "Timestamp",
            "executiontime" => "ExecutionTimeMs",
            "method" => "Method",
            "path" => "Path",
            "statuscode" => "StatusCode",
            "level" => "Level",
            "message" => "Message",
            "query" => "Query",
            _ => "Timestamp"
        };
    }

    private void CleanupCollection<T>(ILiteCollection<T> collection, int maxEntries) where T : DebugEntry
    {
        var count = collection.Count();
        if (count > maxEntries)
        {
            var toDelete = count - maxEntries;
            var oldestEntries = collection.Query()
                .OrderBy(x => x.Timestamp)
                .Limit(toDelete)
                .Select(x => x.Id)
                .ToList();
            
            foreach (var id in oldestEntries)
            {
                collection.Delete(id);
            }
        }
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
