using AspNetDebugDashboard.Core.Models;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

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
            if (prop.PropertyType.IsByRefLike || prop.PropertyType.IsPointer) continue;

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
            // Reading .Result on a Task<T> can block or deadlock the request thread, so treat
            // tasks as opaque leaves and only ever look at their status.
            case Task task:
                node = JsonValue.Create($"{FriendlyTypeName(task.GetType())} ({task.Status})");
                return true;
            case Type type:
                node = JsonValue.Create(type.FullName ?? type.Name);
                return true;
            case Assembly asm:
                node = JsonValue.Create(asm.FullName ?? asm.GetName().Name);
                return true;
            case Module module:
                node = JsonValue.Create(module.Name);
                return true;
            case MemberInfo member:
                node = JsonValue.Create($"{member.DeclaringType?.Name}.{member.Name}");
                return true;
            case Delegate del:
                node = JsonValue.Create($"{del.Method.DeclaringType?.Name}.{del.Method.Name}");
                return true;
            case Stream stream:
                node = JsonValue.Create($"{stream.GetType().Name} (CanRead={stream.CanRead}, CanWrite={stream.CanWrite})");
                return true;
            case CancellationToken token:
                node = JsonValue.Create($"CancellationToken (CanBeCanceled={token.CanBeCanceled}, IsCancellationRequested={token.IsCancellationRequested})");
                return true;
            case IntPtr or UIntPtr:
                node = JsonValue.Create(value.ToString());
                return true;
            case Thread thread:
                node = JsonValue.Create($"Thread {thread.ManagedThreadId} ({thread.Name ?? "unnamed"})");
                return true;
            case WaitHandle:
                node = JsonValue.Create(value.GetType().Name);
                return true;
            // IQueryable enumeration runs a real DB query, so it's never touched here.
            case IQueryable queryable:
                node = JsonValue.Create($"IQueryable<{queryable.ElementType.Name}> (not enumerated)");
                return true;
            default:
                var t = value.GetType();
                if (t == typeof(ValueTask) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                {
                    node = JsonValue.Create($"{FriendlyTypeName(t)} ({ValueTaskStatus(value, t)})");
                    return true;
                }
                node = null;
                return false;
        }
    }

    private static string FriendlyTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(FriendlyTypeName));
        return $"{name}<{args}>";
    }

    private static string ValueTaskStatus(object value, Type type)
    {
        bool Get(string name) => (bool)type.GetProperty(name)!.GetValue(value)!;
        if (!Get("IsCompleted")) return "Pending";
        if (Get("IsFaulted")) return "Faulted";
        if (Get("IsCanceled")) return "Canceled";
        return "RanToCompletion";
    }

    private static bool IsRedacted(string name, DebugConfiguration config) =>
        config.RedactedBodyFields.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
}
