using System.Text.Json;

namespace AspNetDebugDashboard.Mcp;

// Thin wrapper over the dashboard's /_debug/api endpoints. Returns raw JSON
// strings, which is what the agent wants to read anyway.
public sealed class DashboardClient
{
    private readonly HttpClient _http;
    private readonly string _apiBase;

    public DashboardClient(HttpClient http, string baseUrl)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _apiBase = $"{baseUrl.TrimEnd('/')}/_debug/api";
    }

    public async Task<string> GetAsync(string pathAndQuery, CancellationToken ct)
    {
        try
        {
            var res = await _http.GetAsync($"{_apiBase}{pathAndQuery}", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                return Error($"dashboard returned {(int)res.StatusCode}", path: pathAndQuery);
            }
            return body;
        }
        catch (Exception ex)
        {
            return Error(
                $"could not reach the dashboard at {_apiBase}: {ex.Message}",
                hint: "make sure the app is running and DEBUG_DASHBOARD_URL points at it");
        }
    }

    // Serialize rather than hand-build JSON so values with quotes/newlines stay valid.
    private static string Error(string message, string? path = null, string? hint = null)
        => JsonSerializer.Serialize(new { error = message, path, hint });
}
