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
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(keyword => keyword.Trim())
            .Where(keyword => keyword.Length > 0)
            .ToList();
    }

    public static List<string> SplitTokens(string line)
    {
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static string? ExtractComment(string line)
    {
        var index = line.IndexOf('*');
        if (index <= 0)
        {
            return null;
        }

        return line[(index + 1)..].Trim();
    }
}
