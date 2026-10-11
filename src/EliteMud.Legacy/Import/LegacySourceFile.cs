namespace EliteMud.Legacy.Import;

/// <summary>
/// A legacy world file to import.
/// </summary>
/// <param name="Path">Full path on disk.</param>
/// <param name="Name">Folder-relative name, e.g. "wld/57.wld".</param>
/// <param name="IsIndexed">True if the folder's index file lists it, meaning the original game loaded it.</param>
internal sealed record LegacySourceFile(string Path, string Name, bool IsIndexed)
{
    /// <summary>The number in the file name ("57" for "wld/57.wld"), or null if it isn't numeric.</summary>
    public int? Number =>
        int.TryParse(System.IO.Path.GetFileNameWithoutExtension(Path), out var number) ? number : null;
}
