using AspNetDebugDashboard.Core.Models;

namespace AspNetDebugDashboard.Core.Services;

public interface IDebugStorage : IDisposable
{
    Task<string> StoreRequestAsync(RequestEntry request);
    Task<string> StoreSqlQueryAsync(SqlQueryEntry query);
    Task<string> StoreLogAsync(LogEntry log);
    Task<string> StoreExceptionAsync(ExceptionEntry exception);
    
    Task<PagedResult<RequestEntry>> GetRequestsAsync(DebugFilter filter);
    Task<PagedResult<SqlQueryEntry>> GetSqlQueriesAsync(DebugFilter filter);
    Task<PagedResult<LogEntry>> GetLogsAsync(DebugFilter filter);
    Task<PagedResult<ExceptionEntry>> GetExceptionsAsync(DebugFilter filter);
    
    Task<RequestEntry?> GetRequestAsync(string id);
    Task<SqlQueryEntry?> GetSqlQueryAsync(string id);
    Task<LogEntry?> GetLogAsync(string id);
    Task<ExceptionEntry?> GetExceptionAsync(string id);
    
    Task<DebugStats> GetStatsAsync();
    Task CleanupAsync(int maxEntries);
    Task ClearAllAsync();
}
