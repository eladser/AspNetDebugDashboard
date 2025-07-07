using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace AspNetDebugDashboard.Interceptors;

public class DebugCommandInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    
    public DebugCommandInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandAsync(command, eventData, "ExecuteReader");
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutedAsync(command, eventData, "ExecuteReader");
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandAsync(command, eventData, "ExecuteNonQuery");
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutedAsync(command, eventData, "ExecuteNonQuery", result);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandAsync(command, eventData, "ExecuteScalar");
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutedAsync(command, eventData, "ExecuteScalar");
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        await LogCommandErrorAsync(command, eventData);
        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private async Task LogCommandAsync(DbCommand command, CommandEventData eventData, string commandType)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetService<IDebugStorage>();
        var context = scope.ServiceProvider.GetService<DebugContext>();
        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
        
        if (storage == null || context == null) return;
        
        var requestId = httpContextAccessor?.HttpContext?.TraceIdentifier;
        
        var queryEntry = new SqlQueryEntry
        {
            Query = command.CommandText,
            Parameters = GetParameters(command),
            RequestId = requestId ?? string.Empty,
            Database = GetDatabaseName(command),
            ConnectionString = GetSafeConnectionString(command.Connection?.ConnectionString)
        };
        
        // Store the query start time for later duration calculation
        command.CommandText = $"{command.CommandText}/*{queryEntry.Id}:{DateTime.UtcNow.Ticks}*/";
        
        if (!string.IsNullOrEmpty(requestId))
        {
            context.AddSqlQuery(requestId, queryEntry);
        }
    }

    private async Task LogCommandExecutedAsync(DbCommand command, CommandExecutedEventData eventData, string commandType, int? rowsAffected = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetService<IDebugStorage>();
        
        if (storage == null) return;
        
        // Extract query ID and start time from the modified command text
        var (queryId, startTime) = ExtractQueryInfo(command.CommandText);
        
        if (string.IsNullOrEmpty(queryId)) return;
        
        var executionTime = DateTime.UtcNow.Ticks - startTime;
        var executionTimeMs = executionTime / TimeSpan.TicksPerMillisecond;
        
        // Clean up the command text
        command.CommandText = CleanCommandText(command.CommandText);
        
        var queryEntry = new SqlQueryEntry
        {
            Id = queryId,
            Query = command.CommandText,
            Parameters = GetParameters(command),
            ExecutionTimeMs = executionTimeMs,
            RowsAffected = rowsAffected ?? 0,
            Database = GetDatabaseName(command),
            ConnectionString = GetSafeConnectionString(command.Connection?.ConnectionString),
            IsSuccessful = true
        };
        
        await storage.StoreSqlQueryAsync(queryEntry);
    }

    private async Task LogCommandErrorAsync(DbCommand command, CommandErrorEventData eventData)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetService<IDebugStorage>();
        
        if (storage == null) return;
        
        var (queryId, startTime) = ExtractQueryInfo(command.CommandText);
        
        if (string.IsNullOrEmpty(queryId)) return;
        
        var executionTime = DateTime.UtcNow.Ticks - startTime;
        var executionTimeMs = executionTime / TimeSpan.TicksPerMillisecond;
        
        command.CommandText = CleanCommandText(command.CommandText);
        
        var queryEntry = new SqlQueryEntry
        {
            Id = queryId,
            Query = command.CommandText,
            Parameters = GetParameters(command),
            ExecutionTimeMs = executionTimeMs,
            Database = GetDatabaseName(command),
            ConnectionString = GetSafeConnectionString(command.Connection?.ConnectionString),
            IsSuccessful = false,
            Error = eventData.Exception?.Message
        };
        
        await storage.StoreSqlQueryAsync(queryEntry);
    }

    private Dictionary<string, object> GetParameters(DbCommand command)
    {
        var parameters = new Dictionary<string, object>();
        
        foreach (DbParameter parameter in command.Parameters)
        {
            var value = parameter.Value;
            if (value == DBNull.Value)
            {
                value = null;
            }
            
            parameters[parameter.ParameterName] = value ?? "NULL";
        }
        
        return parameters;
    }

    private string? GetDatabaseName(DbCommand command)
    {
        return command.Connection?.Database;
    }

    private string? GetSafeConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;
        
        // Remove sensitive information from connection string
        var parts = connectionString.Split(';');
        var safeParts = new List<string>();
        
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim().ToLower();
                if (key.Contains("password") || key.Contains("pwd") || key.Contains("secret"))
                {
                    safeParts.Add($"{keyValue[0]}=***");
                }
                else
                {
                    safeParts.Add(part);
                }
            }
        }
        
        return string.Join(";", safeParts);
    }

    private (string queryId, long startTime) ExtractQueryInfo(string commandText)
    {
        var lastCommentIndex = commandText.LastIndexOf("/*");
        if (lastCommentIndex == -1) return (string.Empty, 0);
        
        var endCommentIndex = commandText.IndexOf("*/", lastCommentIndex);
        if (endCommentIndex == -1) return (string.Empty, 0);
        
        var comment = commandText.Substring(lastCommentIndex + 2, endCommentIndex - lastCommentIndex - 2);
        var parts = comment.Split(':');
        
        if (parts.Length == 2 && long.TryParse(parts[1], out var startTime))
        {
            return (parts[0], startTime);
        }
        
        return (string.Empty, 0);
    }

    private string CleanCommandText(string commandText)
    {
        var lastCommentIndex = commandText.LastIndexOf("/*");
        if (lastCommentIndex == -1) return commandText;
        
        var endCommentIndex = commandText.IndexOf("*/", lastCommentIndex);
        if (endCommentIndex == -1) return commandText;
        
        return commandText.Substring(0, lastCommentIndex) + commandText.Substring(endCommentIndex + 2);
    }
}
