using System.Text.Json;

namespace EliteMud.Legacy.Import;

/// <summary>
/// Decisions about the legacy files that the index files alone can't make.
/// Stored in content/meta/legacy-import.json.
/// </summary>
public sealed class LegacyImportConfig
{
    /// <summary>Files not to import at all, e.g. old copies of zones that exist elsewhere.</summary>
    public List<SkippedFile> Skip { get; init; } = new();

    /// <summary>Zones the index doesn't list but that are live anyway (the snapshot predates them).</summary>
    public List<int> ActiveZones { get; init; } = new();

    public bool IsSkipped(string fileName, out string reason)
    {
        var match = Skip.FirstOrDefault(skip => string.Equals(skip.File, fileName, StringComparison.OrdinalIgnoreCase));
        reason = match?.Reason ?? string.Empty;
        return match is not null;
    }

    public static LegacyImportConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LegacyImportConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        }) ?? throw new InvalidDataException($"Empty import config: {path}");
    }

    public sealed record SkippedFile(string File, string Reason);
}
