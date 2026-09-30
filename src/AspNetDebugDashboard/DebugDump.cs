using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using System.Runtime.CompilerServices;

namespace AspNetDebugDashboard;

// Static helper for one-off variable dumps from anywhere in the code, including
// places with no HttpContext. Unlike DebugLogger, the sink here is actually wired
// up (by UseDebugDashboard, only when the dashboard is active), so calling this
// before the dashboard starts is a guaranteed no-op instead of a silent stub.
public static class DebugDump
{
    private static Sink? _sink;

    public static void Dump(object? value, string? label = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        var sink = _sink;
        if (sink is null) return;

        var scope = DebugContext.Current.Value;

        var entry = DumpSerializer.Serialize(value, sink.Config);
        entry.Label = label;
        entry.File = file;
        entry.Line = line;
        entry.Member = member;

        if (scope is not null)
        {
            entry.RequestId = scope.RequestId;
            sink.Context.AddDump(scope.RequestId, entry, sink.Config.MaxDumpsPerRequest);
        }

        _ = StoreAsync(sink.Storage, entry);
    }

    private static async Task StoreAsync(IDebugStorage storage, DumpEntry entry)
    {
        try
        {
            await storage.StoreDumpAsync(entry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing dump: {ex.Message}");
        }
    }

    internal static void Attach(IDebugStorage storage, DebugContext context, DebugConfiguration config)
        => _sink = new Sink(storage, context, config);

    internal static void Detach() => _sink = null;

    private sealed record Sink(IDebugStorage Storage, DebugContext Context, DebugConfiguration Config);
}
