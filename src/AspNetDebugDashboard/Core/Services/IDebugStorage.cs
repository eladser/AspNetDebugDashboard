using AspNetDebugDashboard.Core.Models;

namespace AspNetDebugDashboard.Core.Services;

public interface IDebugStorage : IDisposable
{
    // Core CRUD operations
    Task<string> StoreRequestAsync(RequestEntry request);
    Task<string> StoreSqlQueryAsync(SqlQueryEntry query);
    Task<string> StoreLogAsync(LogEntry log);
    Task<string> StoreExceptionAsync(ExceptionEntry exception);
    
    // Retrieval operations
    Task<PagedResult<RequestEntry>> GetRequestsAsync(DebugFilter filter);
    Task<PagedResult<SqlQueryEntry>> GetSqlQueriesAsync(DebugFilter filter);
    Task<PagedResult<LogEntry>> GetLogsAsync(DebugFilter filter);
    Task<PagedResult<ExceptionEntry>> GetExceptionsAsync(DebugFilter filter);
    
    // Single item retrieval
    Task<RequestEntry?> GetRequestAsync(string id);
    Task<SqlQueryEntry?> GetSqlQueryAsync(string id);
    Task<LogEntry?> GetLogAsync(string id);
    Task<ExceptionEntry?> GetExceptionAsync(string id);
    
    // Statistics and analytics
    Task<DebugStats> GetStatsAsync();
    
    // Maintenance operations
    Task CleanupAsync(int maxEntries);
    Task ClearAllAsync();
    
    // Health and monitoring
    Task<object> GetHealthAsync();
    
    // Export/Import operations
    Task<object> ExportAllAsync();
    Task ImportAsync(object data);
    
    // Search operations
    Task<IEnumerable<object>> SearchAsync(string term, string[] types, int maxResults = 50);
    
    // Performance analytics
    Task<object> GetPerformanceMetricsAsync(TimeSpan? timeWindow = null);
    
    // Batch operations
    Task<int> BulkDeleteAsync(string[] ids, string type);
    Task<int> DeleteOlderThanAsync(DateTime cutoff);
    
    // Background maintenance
    Task OptimizeAsync();
    Task<long> GetDatabaseSizeAsync();
    Task<int> GetTotalEntriesAsync();
}
