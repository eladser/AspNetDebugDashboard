using System.Text.Json.Serialization;
using LiteDB;

namespace AspNetFlags;

public class FeatureFlag
{
    [BsonId, JsonIgnore]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string? Description { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FlagsOptions
{
    public bool IsEnabled { get; set; } = true;
    public string BasePath { get; set; } = "/_flags";
    public string DatabasePath { get; set; } = "flags.db";

    // Unknown flags are created (off) the first time code checks them, so they
    // show up in the UI without being declared up front. Turn off to require
    // flags to exist before they can be toggled.
    public bool AutoDiscover { get; set; } = true;
}

/// <summary>Checks and lists feature flags. Inject this wherever you gate features.</summary>
public interface IFeatureFlags
{
    bool IsEnabled(string name);
    IReadOnlyList<FeatureFlag> All();
    void Set(string name, bool enabled);
    bool Remove(string name);
}

public sealed class FeatureFlags : IFeatureFlags, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<FeatureFlag> _col;
    private readonly bool _autoDiscover;
    private readonly object _gate = new();

    public FeatureFlags(FlagsOptions options)
    {
        _db = new LiteDatabase(options.DatabasePath);
        _col = _db.GetCollection<FeatureFlag>("flags");
        _col.EnsureIndex(x => x.Name, unique: true);
        _autoDiscover = options.AutoDiscover;
    }

    public bool IsEnabled(string name)
    {
        lock (_gate)
        {
            var flag = _col.FindOne(x => x.Name == name);
            if (flag != null) return flag.Enabled;
            if (_autoDiscover)
                _col.Insert(new FeatureFlag { Name = name, Enabled = false });
            return false;
        }
    }

    public IReadOnlyList<FeatureFlag> All()
    {
        lock (_gate) return _col.Query().OrderBy(x => x.Name).ToList();
    }

    public void Set(string name, bool enabled)
    {
        lock (_gate)
        {
            var flag = _col.FindOne(x => x.Name == name) ?? new FeatureFlag { Name = name };
            flag.Enabled = enabled;
            flag.UpdatedAt = DateTime.UtcNow;
            _col.Upsert(flag);
        }
    }

    public bool Remove(string name)
    {
        lock (_gate) return _col.DeleteMany(x => x.Name == name) > 0;
    }

    public void Dispose() => _db.Dispose();
}
