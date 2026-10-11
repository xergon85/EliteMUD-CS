namespace EliteMud.Legacy.Import;

/// <summary>
/// Collects imported records and resolves duplicate ids like the original game:
/// a later definition replaces an earlier one (load_rooms() in db.c), except that a file
/// the original didn't load never replaces one it did. Every duplicate is logged.
/// </summary>
internal sealed class LegacyRecordSet<T>
    where T : class
{
    private readonly string _kind;
    private readonly LegacyImportLog _log;
    private readonly List<T> _items = new();
    private readonly Dictionary<int, (int Position, LegacySourceFile Source)> _byId = new();

    public LegacyRecordSet(string kind, LegacyImportLog log)
    {
        _kind = kind;
        _log = log;
    }

    public void Add(int id, T item, LegacySourceFile source)
    {
        if (!_byId.TryGetValue(id, out var existing))
        {
            _byId[id] = (_items.Count, source);
            _items.Add(item);
            return;
        }

        var replace = source.IsIndexed || !existing.Source.IsIndexed;
        var kept = replace ? source.Name : existing.Source.Name;
        _log.Warn("Duplicate id", source.Name, $"{_kind} #{id} is also defined in {existing.Source.Name}; keeping the one in {kept}");

        if (replace)
        {
            _items[existing.Position] = item;
            _byId[id] = (existing.Position, source);
        }
    }

    public List<T> ToList() => new(_items);
}
