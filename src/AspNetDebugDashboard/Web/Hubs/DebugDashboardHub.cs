using Microsoft.AspNetCore.SignalR;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.Extensions.Options;

namespace AspNetDebugDashboard.Web.Hubs;

public class DebugDashboardHub : Hub
{
    private readonly DebugConfiguration _config;

    public DebugDashboardHub(IOptions<DebugConfiguration> config)
    {
        _config = config.Value;
    }

    public async Task JoinGroup(string groupName)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        if (_config.IsEnabled && _config.EnableRealTimeUpdates)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard-users");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard-users");
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IDebugDashboardNotificationService
{
    Task NotifyNewRequestAsync(RequestEntry request);
    Task NotifyNewSqlQueryAsync(SqlQueryEntry query);
    Task NotifyNewLogAsync(LogEntry log);
    Task NotifyNewExceptionAsync(ExceptionEntry exception);
    Task NotifyStatsUpdatedAsync(DebugStats stats);
    Task NotifyDataClearedAsync();
}

public class DebugDashboardNotificationService : IDebugDashboardNotificationService
{
    private readonly IHubContext<DebugDashboardHub> _hubContext;
    private readonly DebugConfiguration _config;

    public DebugDashboardNotificationService(
        IHubContext<DebugDashboardHub> hubContext, 
        IOptions<DebugConfiguration> config)
    {
        _hubContext = hubContext;
        _config = config.Value;
    }

    public async Task NotifyNewRequestAsync(RequestEntry request)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("NewRequest", new
            {
                Id = request.Id,
                Method = request.Method,
                Path = request.Path,
                StatusCode = request.StatusCode,
                ExecutionTimeMs = request.ExecutionTimeMs,
                Timestamp = request.Timestamp
            });
    }

    public async Task NotifyNewSqlQueryAsync(SqlQueryEntry query)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("NewSqlQuery", new
            {
                Id = query.Id,
                Query = query.Query.Length > 100 ? query.Query[..100] + "..." : query.Query,
                ExecutionTimeMs = query.ExecutionTimeMs,
                IsSuccessful = query.IsSuccessful,
                Timestamp = query.Timestamp
            });
    }

    public async Task NotifyNewLogAsync(LogEntry log)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("NewLog", new
            {
                Id = log.Id,
                Message = log.Message,
                Level = log.Level,
                Tag = log.Tag,
                Timestamp = log.Timestamp
            });
    }

    public async Task NotifyNewExceptionAsync(ExceptionEntry exception)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("NewException", new
            {
                Id = exception.Id,
                Message = exception.Message,
                ExceptionType = exception.ExceptionType,
                Method = exception.Method,
                Path = exception.Path,
                Timestamp = exception.Timestamp
            });
    }

    public async Task NotifyStatsUpdatedAsync(DebugStats stats)
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("StatsUpdated", stats);
    }

    public async Task NotifyDataClearedAsync()
    {
        if (!_config.IsEnabled || !_config.EnableRealTimeUpdates)
            return;

        await _hubContext.Clients.Group("dashboard-users")
            .SendAsync("DataCleared");
    }
}
