namespace EliteMud.Legacy.Import;

/// <summary>
/// Collects everything the importer skipped, guessed or resolved, so nothing is dropped silently.
/// </summary>
public sealed class LegacyImportLog
{
    private readonly List<LegacyImportMessage> _messages = new();

    public IReadOnlyList<LegacyImportMessage> Messages => _messages;

    public void Warn(string category, string source, string message) =>
        Add(new LegacyImportMessage(LegacyImportSeverity.Warning, category, source, message));

    public void Info(string category, string source, string message) =>
        Add(new LegacyImportMessage(LegacyImportSeverity.Info, category, source, message));

    private void Add(LegacyImportMessage message)
    {
        _messages.Add(message);
        if (message.Severity == LegacyImportSeverity.Warning)
        {
            Console.WriteLine($"  Warning: {message.Source}: {message.Text}");
        }
    }

    /// <summary>One line per category with its count, warnings first.</summary>
    public IEnumerable<string> Summarize() =>
        _messages
            .GroupBy(message => (message.Severity, message.Category))
            .OrderBy(group => group.Key.Severity)
            .ThenBy(group => group.Key.Category)
            .Select(group => $"{group.Key.Severity,-7} {group.Count(),5}  {group.Key.Category}");

    public async ValueTask SaveAsync(string path, CancellationToken cancellationToken)
    {
        var lines = _messages
            .OrderBy(message => message.Severity)
            .ThenBy(message => message.Category)
            .ThenBy(message => message.Source, StringComparer.Ordinal)
            .Select(message => $"{message.Severity}\t{message.Category}\t{message.Source}\t{message.Text}");
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }
}

public enum LegacyImportSeverity
{
    Warning,
    Info
}

public sealed record LegacyImportMessage(
    LegacyImportSeverity Severity,
    string Category,
    string Source,
    string Text);
