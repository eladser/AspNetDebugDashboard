using Microsoft.AspNetCore.Http;

namespace AspNetDebugDashboard.Middleware;

// The dashboard's replay button sends this header with the id of the entry it replays.
// It's never stored as a regular header, so replaying a replay doesn't send it twice.
internal static class ReplayMarker
{
    public const string Header = "X-Debug-Replay-Of";

    public static bool IsHeader(string name) => name.Equals(Header, StringComparison.OrdinalIgnoreCase);

    public static string? Read(HttpRequest request) =>
        Guid.TryParse(request.Headers[Header].ToString(), out var id) ? id.ToString() : null;
}
