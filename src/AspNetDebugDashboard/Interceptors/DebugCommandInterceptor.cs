using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace AspNetDebugDashboard.Interceptors;

// Records executed commands from EF's Executed/Failed callbacks. The command
// itself is never touched: mutating CommandText mid-pipeline breaks providers
// (SQLite throws if a reader is open) and changes what the database sees.
public class DebugCommandInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public DebugCommandInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(command, eventData.Duration, true, null, null);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogFireAndForget(command, eventData.Duration, true, null, null);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(command, eventData.Duration, true, null, result);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogFireAndForget(command, eventData.Duration, true, null, result);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(command, eventData.Duration, true, null, null);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogFireAndForget(command, eventData.Duration, true, null, null);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        await LogAsync(command, eventData.Duration, false, eventData.Exception?.Message, null);
        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogFireAndForget(command, eventData.Duration, false, eventData.Exception?.Message, null);
        base.CommandFailed(command, eventData);
    }

    private void LogFireAndForget(DbCommand command, TimeSpan duration, bool success, string? error, int? rowsAffected)
    {
        var entry = BuildEntry(command, duration, success, error, rowsAffected);
        if (entry == null) return;
        _ = Task.Run(() => StoreAsync(entry));
    }

    private async Task LogAsync(DbCommand command, TimeSpan duration, bool success, string? error, int? rowsAffected)
    {
        var entry = BuildEntry(command, duration, success, error, rowsAffected);
        if (entry == null) return;
        await StoreAsync(entry);
    }

    // Snapshot everything we need from the DbCommand synchronously — it may be
    // disposed or reused by the time a background store task runs.
    private SqlQueryEntry? BuildEntry(DbCommand command, TimeSpan duration, bool success, string? error, int? rowsAffected)
    {
        var config = _serviceProvider.GetService<IOptions<DebugConfiguration>>()?.Value;
        if (config is not { IsEnabled: true, LogSqlQueries: true }) return null;

        var httpContextAccessor = _serviceProvider.GetService<IHttpContextAccessor>();
        var requestId = httpContextAccessor?.HttpContext?.TraceIdentifier;
        var ms = (long)duration.TotalMilliseconds;

        return new SqlQueryEntry
        {
            Query = command.CommandText,
            Parameters = GetParameters(command),
            ExecutionTimeMs = ms,
            RowsAffected = rowsAffected ?? 0,
            RequestId = requestId ?? string.Empty,
            Database = command.Connection?.Database,
            IsSuccessful = success,
            Error = error,
            IsSlowQuery = ms >= config.SlowQueryThresholdMs,
        };
    }

    private async Task StoreAsync(SqlQueryEntry entry)
    {
        try
        {
            var storage = _serviceProvider.GetService<IDebugStorage>();
            if (storage == null) return;

            await storage.StoreSqlQueryAsync(entry);

            if (!string.IsNullOrEmpty(entry.RequestId))
            {
                _serviceProvider.GetService<DebugContext>()?.AddSqlQuery(entry.RequestId, entry);
            }
        }
        catch
        {
            // a failure to record a query must never take the app down with it
        }
    }

    private static Dictionary<string, object> GetParameters(DbCommand command)
    {
        var parameters = new Dictionary<string, object>();
        foreach (DbParameter parameter in command.Parameters)
        {
            var value = parameter.Value;
            parameters[parameter.ParameterName] = value == null || value == DBNull.Value ? "NULL" : value;
        }
        return parameters;
    }
}
