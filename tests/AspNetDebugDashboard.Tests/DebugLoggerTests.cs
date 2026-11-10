using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugLoggerTests : IDisposable
{
    public DebugLoggerTests() => DebugLogger.SetServiceProvider(null);
    public void Dispose() => DebugLogger.SetServiceProvider(null);

    [Fact]
    public async Task InfoAsync_WhileInactive_DoesNothing()
    {
        var act = () => DebugLogger.InfoAsync("hello");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InfoAsync_WhileActive_StoresAndAttachesToTheCurrentRequest()
    {
        var storage = new Mock<IDebugStorage>();
        storage.Setup(s => s.StoreLogAsync(It.IsAny<LogEntry>())).ReturnsAsync("id");

        var context = new DebugContext();
        var httpContextAccessor = new HttpContextAccessor();

        var services = new ServiceCollection();
        services.AddSingleton(httpContextAccessor);
        services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
        services.AddSingleton(context);
        services.AddSingleton(storage.Object);
        services.AddScoped<IDebugLogger, Core.Services.DebugLogger>();
        var provider = services.BuildServiceProvider();

        DebugLogger.SetServiceProvider(provider);

        const string requestId = "req-log-1";
        context.StartRequest(requestId, new RequestEntry { RequestId = requestId });
        httpContextAccessor.HttpContext = new DefaultHttpContext { TraceIdentifier = requestId, RequestServices = provider };

        await DebugLogger.InfoAsync("hello", "tag");

        storage.Verify(s => s.StoreLogAsync(It.Is<LogEntry>(l => l.Message == "hello" && l.Tag == "tag" && l.RequestId == requestId)), Times.Once);

        var completed = context.CompleteRequest(requestId);
        completed!.Logs.Should().ContainSingle(l => l.Message == "hello");
    }
}
