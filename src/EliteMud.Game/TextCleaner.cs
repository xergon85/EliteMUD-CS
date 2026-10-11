namespace EliteMud.Game;

/// <summary>
/// Utility for cleaning legacy text strings.
/// Removes newlines, carriage returns, tabs, and escape sequences.
/// </summary>
public static class TextCleaner
{
    /// <summary>
    /// Clean string by removing newlines, carriage returns, tabs.
    /// Legacy importer sometimes includes these in descriptions.
    /// </summary>
    public static string Clean(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        
        return input
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("\t", " ")
            .Replace("\\n", " ")
            .Replace("\\r", "")
            .Replace("\\t", " ")
            .Trim();
    }

    /// <summary>
    /// Clean a multi-line text (room, mob and object descriptions) but keep its line breaks. Legacy text is
    /// hand-wrapped at about 75 columns, so it's shown line by line as written; joining the lines would leave
    /// the client to break one long line mid-word. Line endings become "\n" (the telnet layer sends "\r\n"),
    /// tabs become spaces, and trailing spaces and blank lines at the start and end are removed.
    /// </summary>
    public static string CleanMultiline(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = input
            .Replace("\\r", "")
            .Replace("\\n", "\n")
            .Replace("\\t", " ")
            .Replace("\r", "")
            .Replace("\t", " ");
        var lines = text.Split('\n').Select(line => line.TrimEnd());
        return string.Join('\n', lines).Trim('\n');
    }
}
