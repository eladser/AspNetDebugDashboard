using LiteDB;

namespace AspNetMailbox;

public interface IMailboxStore
{
    void Save(MailboxMessage message);
    IReadOnlyList<MailboxMessage> List(string? search, int skip, int take);
    int Count(string? search);
    MailboxMessage? Get(string id);
    void Clear();
}

public sealed class LiteDbMailboxStore : IMailboxStore, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<MailboxMessage> _col;
    private readonly int _max;
    private readonly object _gate = new();

    public LiteDbMailboxStore(MailboxOptions options)
    {
        _db = new LiteDatabase(options.DatabasePath);
        _col = _db.GetCollection<MailboxMessage>("messages");
        _col.EnsureIndex(x => x.ReceivedAt);
        _max = options.MaxMessages;
    }

    public void Save(MailboxMessage message)
    {
        lock (_gate)
        {
            _col.Insert(message);
            // trim oldest beyond the cap
            var count = _col.Count();
            if (count > _max)
            {
                var stale = _col.Query().OrderBy(x => x.ReceivedAt).Limit(count - _max).ToList();
                foreach (var m in stale) _col.Delete(m.Id);
            }
        }
    }

    public IReadOnlyList<MailboxMessage> List(string? search, int skip, int take)
    {
        lock (_gate)
        {
            var q = _col.Query();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.Subject.Contains(search) || x.From.Contains(search));
            return q.OrderByDescending(x => x.ReceivedAt).Skip(skip).Limit(take).ToList();
        }
    }

    public int Count(string? search)
    {
        lock (_gate)
        {
            var q = _col.Query();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.Subject.Contains(search) || x.From.Contains(search));
            return q.Count();
        }
    }

    public MailboxMessage? Get(string id) { lock (_gate) return _col.FindById(id); }

    public void Clear() { lock (_gate) _col.DeleteAll(); }

    public void Dispose() => _db.Dispose();
}
