using AspNetDebugDashboard.Core.Models;
using System.Collections.Concurrent;

namespace AspNetDebugDashboard.Core.Services;

public class DebugContext
{
    private readonly ConcurrentDictionary<string, RequestEntry> _activeRequests = new();
    private readonly ConcurrentDictionary<string, List<SqlQueryEntry>> _requestSqlQueries = new();
    private readonly ConcurrentDictionary<string, List<LogEntry>> _requestLogs = new();
    
    public void StartRequest(string requestId, RequestEntry request)
    {
        _activeRequests.TryAdd(requestId, request);
        _requestSqlQueries.TryAdd(requestId, new List<SqlQueryEntry>());
        _requestLogs.TryAdd(requestId, new List<LogEntry>());
    }
    
    public RequestEntry? GetActiveRequest(string requestId)
    {
        _activeRequests.TryGetValue(requestId, out var request);
        return request;
    }
    
    public void AddSqlQuery(string requestId, SqlQueryEntry query)
    {
        if (_requestSqlQueries.TryGetValue(requestId, out var queries))
        {
            queries.Add(query);
        }
    }
    
    public void AddLog(string requestId, LogEntry log)
    {
        if (_requestLogs.TryGetValue(requestId, out var logs))
        {
            logs.Add(log);
        }
    }
    
    public RequestEntry? CompleteRequest(string requestId)
    {
        if (_activeRequests.TryRemove(requestId, out var request))
        {
            if (_requestSqlQueries.TryRemove(requestId, out var queries))
            {
                request.SqlQueries = queries;
            }
            
            if (_requestLogs.TryRemove(requestId, out var logs))
            {
                request.Logs = logs;
            }
            
            return request;
        }
        
        return null;
    }
    
    public void SetException(string requestId, ExceptionEntry exception)
    {
        if (_activeRequests.TryGetValue(requestId, out var request))
        {
            request.Exception = exception;
        }
    }
}
