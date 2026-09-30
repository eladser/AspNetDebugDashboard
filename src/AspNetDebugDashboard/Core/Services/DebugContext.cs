using AspNetDebugDashboard.Core.Models;
using System.Collections.Concurrent;

namespace AspNetDebugDashboard.Core.Services;

public record RequestScope(string RequestId, string? TraceId);

public class DebugContext
{
    // Ambient handle to the in-flight request, set by DebugRequestMiddleware. Lets code
    // that has no HttpContext (outgoing HTTP capture, DebugDump) find its way back to it.
    public static readonly AsyncLocal<RequestScope?> Current = new();

    private readonly ConcurrentDictionary<string, RequestEntry> _activeRequests = new();
    private readonly ConcurrentDictionary<string, List<SqlQueryEntry>> _requestSqlQueries = new();
    private readonly ConcurrentDictionary<string, List<LogEntry>> _requestLogs = new();
    private readonly ConcurrentDictionary<string, List<OutgoingHttpEntry>> _requestOutgoingHttp = new();
    private readonly ConcurrentDictionary<string, List<DumpEntry>> _requestDumps = new();
    
    public void StartRequest(string requestId, RequestEntry request)
    {
        _activeRequests.TryAdd(requestId, request);
        _requestSqlQueries.TryAdd(requestId, new List<SqlQueryEntry>());
        _requestLogs.TryAdd(requestId, new List<LogEntry>());
        _requestOutgoingHttp.TryAdd(requestId, new List<OutgoingHttpEntry>());
        _requestDumps.TryAdd(requestId, new List<DumpEntry>());
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
    
    public void AddOutgoingHttp(string requestId, OutgoingHttpEntry entry)
    {
        if (_requestOutgoingHttp.TryGetValue(requestId, out var entries))
        {
            entries.Add(entry);
        }
    }
    
    public void AddDump(string requestId, DumpEntry dump)
    {
        if (_requestDumps.TryGetValue(requestId, out var dumps))
        {
            dumps.Add(dump);
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
            
            if (_requestOutgoingHttp.TryRemove(requestId, out var outgoingHttp))
            {
                request.OutgoingHttp = outgoingHttp;
            }
            
            if (_requestDumps.TryRemove(requestId, out var dumps))
            {
                request.Dumps = dumps;
            }
            
            return request;
        }
        
        return null;
    }
    
    public void SetException(string requestId, ExceptionEntry exception)
    {
        if (_activeRequests.TryGetValue(requestId, out var request))
        {
            request.Exception = exception.Message; // Convert to string
        }
    }
}
