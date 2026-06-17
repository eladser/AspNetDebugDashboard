using System.Text.Json;

namespace AspNetDebugDashboard.Mcp;

// Thin wrapper over the dashboard's /_debug/api endpoints. Returns raw JSON
// strings, which is what the agent wants to read anyway.
public sealed class DashboardClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiBase;

    public DashboardClient(HttpClient http, string baseUrl)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _baseUrl = baseUrl.TrimEnd('/');
        _apiBase = $"{_baseUrl}/_debug/api";
    }

    // dashboard endpoints under /_debug/api
    public Task<string> GetAsync(string pathAndQuery, CancellationToken ct)
        => GetAtAsync($"{_apiBase}{pathAndQuery}", ct);

    // any suite-tool endpoint, e.g. "/_flags/api/flags" or "/_vitals/api/vitals"
    public Task<string> GetSuiteAsync(string pathAndQuery, CancellationToken ct)
        => GetAtAsync($"{_baseUrl}{pathAndQuery}", ct);

    private async Task<string> GetAtAsync(string url, CancellationToken ct)
    {
        try
        {
            var res = await _http.GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                return Error($"{(int)res.StatusCode} from {url}",
                    hint: "that tool may not be installed or the route differs");
            }
            return body;
        }
        catch (Exception ex)
        {
            return Error(
                $"could not reach {url}: {ex.Message}",
                hint: "make sure the app is running and DEBUG_DASHBOARD_URL points at it");
        }
    }

    // Serialize rather than hand-build JSON so values with quotes/newlines stay valid.
    private static string Error(string message, string? path = null, string? hint = null)
        => JsonSerializer.Serialize(new { error = message, path, hint });
}
