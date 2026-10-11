namespace EliteMud.Legacy.Import;

/// <summary>
/// Lists the files to import from one legacy world folder.
/// Legacy: index_boot() in db.c only loads the files named in the folder's "index" file, in that order.
/// </summary>
internal static class LegacyWorldFiles
{
    public static IReadOnlyList<LegacySourceFile> Resolve(
        string folderPath,
        string extension,
        LegacyImportConfig config,
        LegacyImportLog log)
    {
        var folder = System.IO.Path.GetFileName(folderPath);
        var onDisk = Directory.EnumerateFiles(folderPath, $"*.{extension}")
            .Select(System.IO.Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var indexPath = System.IO.Path.Combine(folderPath, "index");
        var indexed = File.Exists(indexPath) ? ReadIndex(indexPath) : null;
        if (indexed is null)
        {
            log.Warn("No index file", folder, "no index file; importing every file in name order");
        }

        var files = new List<LegacySourceFile>();
        foreach (var fileName in indexed ?? new List<string>())
        {
            if (!onDisk.Contains(fileName))
            {
                log.Warn("Index lists missing file", $"{folder}/index", $"lists {fileName}, which doesn't exist");
                continue;
            }

            AddUnlessSkipped(files, folderPath, folder, fileName, isIndexed: true, config, log);
        }

        var notIndexed = onDisk
            .Where(fileName => indexed is null || !indexed.Contains(fileName))
            .OrderBy(fileName => fileName, StringComparer.Ordinal);
        foreach (var fileName in notIndexed)
        {
            AddUnlessSkipped(files, folderPath, folder, fileName, isIndexed: indexed is null, config, log);
        }

        return files;
    }

    private static void AddUnlessSkipped(
        List<LegacySourceFile> files,
        string folderPath,
        string folder,
        string fileName,
        bool isIndexed,
        LegacyImportConfig config,
        LegacyImportLog log)
    {
        var name = $"{folder}/{fileName}";
        if (config.IsSkipped(name, out var reason))
        {
            log.Info("Skipped file", name, reason);
            return;
        }

        if (!isIndexed)
        {
            log.Info("Not in index", name, "the original game didn't load this file; imported as inactive");
        }

        files.Add(new LegacySourceFile(System.IO.Path.Combine(folderPath, fileName), name, isIndexed));
    }

    private static List<string> ReadIndex(string indexPath) =>
        File.ReadLines(indexPath, System.Text.Encoding.Latin1)
            .Select(line => line.Trim())
            .TakeWhile(line => !line.StartsWith('$')) // "$" or "$~" ends the list
            .Where(line => line.Length > 0)
            .ToList();
}
