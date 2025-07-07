using AspNetDebugDashboard.Core.Models;

namespace AspNetDebugDashboard.Core.Services;

/// <summary>
/// Service for managing debug dashboard operations
/// </summary>
public interface IDebugDashboardService
{
    /// <summary>
    /// Gets the current dashboard statistics
    /// </summary>
    Task<DebugStats> GetStatisticsAsync();
    
    /// <summary>
    /// Exports debug data to a file
    /// </summary>
    Task<byte[]> ExportDataAsync(ExportFormat format, DebugFilter? filter = null);
    
    /// <summary>
    /// Imports debug data from a file
    /// </summary>
    Task ImportDataAsync(byte[] data, ExportFormat format);
    
    /// <summary>
    /// Clears all debug data
    /// </summary>
    Task ClearAllDataAsync();
    
    /// <summary>
    /// Performs cleanup of old entries
    /// </summary>
    Task CleanupOldDataAsync();
}

public enum ExportFormat
{
    Json,
    Csv,
    Excel
}

public class DebugDashboardService : IDebugDashboardService
{
    private readonly IDebugStorage _storage;
    private readonly DebugConfiguration _config;
    
    public DebugDashboardService(IDebugStorage storage, Microsoft.Extensions.Options.IOptions<DebugConfiguration> config)
    {
        _storage = storage;
        _config = config.Value;
    }
    
    public async Task<DebugStats> GetStatisticsAsync()
    {
        return await _storage.GetStatsAsync();
    }
    
    public async Task<byte[]> ExportDataAsync(ExportFormat format, DebugFilter? filter = null)
    {
        filter ??= new DebugFilter { Page = 1, PageSize = 10000 };
        
        var requests = await _storage.GetRequestsAsync(filter);
        var queries = await _storage.GetSqlQueriesAsync(filter);
        var logs = await _storage.GetLogsAsync(filter);
        var exceptions = await _storage.GetExceptionsAsync(filter);
        
        var data = new
        {
            Requests = requests.Items,
            SqlQueries = queries.Items,
            Logs = logs.Items,
            Exceptions = exceptions.Items,
            ExportedAt = DateTime.UtcNow
        };
        
        return format switch
        {
            ExportFormat.Json => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            ExportFormat.Csv => throw new NotImplementedException("CSV export not yet implemented"),
            ExportFormat.Excel => throw new NotImplementedException("Excel export not yet implemented"),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }
    
    public async Task ImportDataAsync(byte[] data, ExportFormat format)
    {
        // Implementation for importing data
        throw new NotImplementedException("Import functionality not yet implemented");
    }
    
    public async Task ClearAllDataAsync()
    {
        await _storage.ClearAllAsync();
    }
    
    public async Task CleanupOldDataAsync()
    {
        await _storage.CleanupAsync(_config.MaxEntries);
    }
}
