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
}
