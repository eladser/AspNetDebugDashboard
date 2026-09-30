using AspNetDebugDashboard.Core.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspNetDebugDashboard.Core.Services;

// Walks an arbitrary object graph into a JsonNode snapshot, taken synchronously at the
// call site (not deferred, so it reflects state at the moment of the Dump() call). Uses
// its own depth/item/cycle bookkeeping instead of System.Text.Json's reflection
// serializer, which throws on cycles and on MaxDepth instead of truncating.
public static class DumpSerializer
{
    public static DumpEntry Serialize(object? value, DebugConfiguration config)
    {
        var truncated = false;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var node = Walk(value, config, seen, 0, ref truncated);

        var entry = new DumpEntry
        {
            TypeName = value?.GetType().FullName ?? "null",
        };

        var json = node?.ToJsonString() ?? "null";
        if (json.Length > config.DumpMaxSize)
        {
            json = json[..config.DumpMaxSize];
            truncated = true;
        }

        entry.Json = json;
        entry.Truncated = truncated;
        return entry;
    }

    private static JsonNode? Walk(object? value, DebugConfiguration config, HashSet<object> seen, int depth, ref bool truncated)
    {
        if (value is null) return null;

        if (TryLeaf(value, out var leaf)) return leaf;

        if (depth >= config.DumpMaxDepth)
        {
            truncated = true;
            return JsonValue.Create($"[max depth reached: {value.GetType().Name}]");
        }

        var isRef = !value.GetType().IsValueType;
        if (isRef && !seen.Add(value))
        {
            return JsonValue.Create("[circular reference]");
        }

        try
        {
            if (value is IDictionary dict)
            {
                var obj = new JsonObject();
                var count = 0;
                foreach (DictionaryEntry kv in dict)
                {
                    if (count >= config.DumpMaxItems)
                    {
                        truncated = true;
                        break;
                    }
                    var key = kv.Key?.ToString() ?? "null";
                    obj[key] = IsRedacted(key, config) ? JsonValue.Create("***") : Walk(kv.Value, config, seen, depth + 1, ref truncated);
                    count++;
                }
                return obj;
            }

            if (value is IEnumerable enumerable)
            {
                var arr = new JsonArray();
                var count = 0;
                foreach (var item in enumerable)
                {
                    if (count >= config.DumpMaxItems)
                    {
                        truncated = true;
                        break;
                    }
                    arr.Add(Walk(item, config, seen, depth + 1, ref truncated));
                    count++;
                }
                return arr;
            }

            return WalkObject(value, config, seen, depth, ref truncated);
        }
        finally
        {
            if (isRef) seen.Remove(value);
        }
    }

    private static JsonNode WalkObject(object value, DebugConfiguration config, HashSet<object> seen, int depth, ref bool truncated)
    {
        var obj = new JsonObject();
        var props = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var count = 0;

        foreach (var prop in props)
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;

            if (count >= config.DumpMaxItems)
            {
                truncated = true;
                break;
            }
            count++;

            if (IsRedacted(prop.Name, config))
            {
                obj[prop.Name] = JsonValue.Create("***");
                continue;
            }

            object? propValue;
            try
            {
                propValue = prop.GetValue(value);
            }
            catch (Exception ex)
            {
                var real = ex.InnerException ?? ex;
                obj[prop.Name] = JsonValue.Create($"[threw {real.GetType().Name}: {real.Message}]");
                continue;
            }

            obj[prop.Name] = Walk(propValue, config, seen, depth + 1, ref truncated);
        }

        return obj;
    }

    private static bool TryLeaf(object value, out JsonNode? node)
    {
        switch (value)
        {
            case string s:
                node = JsonValue.Create(s);
                return true;
            case bool b:
                node = JsonValue.Create(b);
                return true;
            case byte[] bytes:
                node = JsonValue.Create(Convert.ToBase64String(bytes));
                return true;
            case Enum e:
                node = JsonValue.Create(e.ToString());
                return true;
            case Guid or TimeSpan:
                node = JsonValue.Create(value.ToString());
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                or DateTime or DateTimeOffset:
                node = JsonSerializer.SerializeToNode(value, value.GetType());
                return true;
            default:
                node = null;
                return false;
        }
    }

    private static bool IsRedacted(string name, DebugConfiguration config) =>
        config.RedactedBodyFields.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
}
