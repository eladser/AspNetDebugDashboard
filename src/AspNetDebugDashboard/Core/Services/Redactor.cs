using System.Text;
using System.Text.Json;

namespace AspNetDebugDashboard.Core.Services;

// Shared by the request middleware and the outgoing HTTP capture: replaces sensitive
// values in bodies and URLs with "***" before anything reaches storage.
public static class Redactor
{
    // Replaces password/token/etc. field values in a JSON or form-urlencoded body with
    // "***" before it gets stored. Other bodies are left as they are.
    public static string RedactBody(string body, IReadOnlyCollection<string> redactedFields)
    {
        if (string.IsNullOrWhiteSpace(body) || redactedFields.Count == 0) return body;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var redacted = false;
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteRedacted(doc.RootElement, writer, redactedFields, ref redacted);
            }

            // Nothing matched: return the original bytes untouched instead of a
            // re-serialized (differently-formatted) copy.
            return redacted ? Encoding.UTF8.GetString(stream.ToArray()) : body;
        }
        catch (JsonException)
        {
            return RedactFormFields(body, redactedFields);
        }
    }

    // Strips userinfo and redacts query parameters matching redactedFields. Returns the
    // original string untouched when there's nothing to redact, so normal URLs never get
    // reformatted by round-tripping through UriBuilder.
    public static string RedactUrl(string url, IReadOnlyCollection<string> redactedFields)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

        var hasUserInfo = !string.IsNullOrEmpty(uri.UserInfo);
        var redactedQuery = RedactQuery(uri.Query, redactedFields, out var queryChanged);

        if (!hasUserInfo && !queryChanged) return url;

        var builder = new UriBuilder(uri) { UserName = "", Password = "" };
        if (queryChanged) builder.Query = redactedQuery.TrimStart('?');
        return builder.Uri.ToString();
    }

    private static string RedactQuery(string query, IReadOnlyCollection<string> redactedFields, out bool changed)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            changed = false;
            return query;
        }

        var anyChanged = false;
        var pairs = query.TrimStart('?').Split('&').Select(pair =>
        {
            var eq = pair.IndexOf('=');
            var name = eq < 0 ? pair : pair[..eq];
            if (!IsSensitiveField(Uri.UnescapeDataString(name), redactedFields)) return pair;
            anyChanged = true;
            return eq < 0 ? pair : pair[..eq] + "=***";
        }).ToList();

        changed = anyChanged;
        return string.Join('&', pairs);
    }

    // a=1&password=x style bodies (HTML form logins). Real form bodies never contain raw
    // whitespace, which keeps plain text and XML out of this path.
    private static string RedactFormFields(string body, IReadOnlyCollection<string> redactedFields)
    {
        if (!body.Contains('=') || body.Any(char.IsWhiteSpace)) return body;

        var redacted = false;
        var pairs = body.Split('&').Select(pair =>
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0 || !IsSensitiveField(Uri.UnescapeDataString(pair[..eq].Replace('+', ' ')), redactedFields)) return pair;
            redacted = true;
            return pair[..eq] + "=***";
        }).ToList();

        return redacted ? string.Join('&', pairs) : body;
    }

    private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer, IReadOnlyCollection<string> redactedFields, ref bool redacted)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    if (IsSensitiveField(prop.Name, redactedFields))
                    {
                        writer.WriteStringValue("***");
                        redacted = true;
                    }
                    else
                    {
                        WriteRedacted(prop.Value, writer, redactedFields, ref redacted);
                    }
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedacted(item, writer, redactedFields, ref redacted);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool IsSensitiveField(string name, IReadOnlyCollection<string> redactedFields) =>
        redactedFields.Any(field => name.Contains(field, StringComparison.OrdinalIgnoreCase));
}
