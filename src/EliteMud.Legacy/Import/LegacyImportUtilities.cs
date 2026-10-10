using System.Text;

namespace EliteMud.Legacy.Import;

internal static class LegacyImportUtilities
{
    public static StreamReader CreateReader(string path)
    {
        return new StreamReader(path, Encoding.Latin1);
    }

    public static List<string> SplitKeywords(string keywords)
    {
        return keywords
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(keyword => keyword.Trim())
            .Where(keyword => keyword.Length > 0)
            .ToList();
    }

    public static List<string> SplitTokens(string line)
    {
        // Split on any whitespace: legacy files mix spaces and tabs.
        return line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
