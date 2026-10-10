using System.Diagnostics.CodeAnalysis;

namespace EliteMud.Legacy.Import;

/// <summary>
/// Parses one zone reset command line.
/// Legacy: load_reset_cmds() in db.c:1109-1159
///
/// Format: command letter, if-flag, arg1, arg2, then arg3 only for commands that take one.
/// Anything after the last argument is a free-form comment (often separated by tabs).
/// </summary>
internal static class LegacyZoneResetParser
{
    /// <summary>
    /// Commands that read a third argument (legacy: M, O, E, P, L, A, D).
    /// G and R take only two, so a trailing number after them is part of the comment.
    /// </summary>
    private const string CommandsWithArg3 = "MOEPLAD";

    public static bool TryParse(string line, [NotNullWhen(true)] out ZoneResetCommandContent? command)
    {
        command = null;

        var tokens = LegacyImportUtilities.SplitTokens(line);
        if (tokens.Count < 4 || tokens[0].Length != 1)
        {
            return false;
        }

        var letter = tokens[0];
        if (!int.TryParse(tokens[1], out var ifFlag)
            || !int.TryParse(tokens[2], out var arg1)
            || !int.TryParse(tokens[3], out var arg2))
        {
            return false;
        }

        var argumentCount = 3;
        int? arg3 = null;
        if (CommandsWithArg3.Contains(letter[0]))
        {
            if (tokens.Count < 5 || !int.TryParse(tokens[4], out var parsedArg3))
            {
                return false;
            }

            arg3 = parsedArg3;
            argumentCount = 4;
        }

        var comment = ExtractTrailingComment(tokens, argumentCount + 1);
        command = new ZoneResetCommandContent(letter, ifFlag, arg1, arg2, arg3, comment);
        return true;
    }

    private static string? ExtractTrailingComment(List<string> tokens, int firstCommentToken)
    {
        if (tokens.Count <= firstCommentToken)
        {
            return null;
        }

        var comment = string.Join(' ', tokens.Skip(firstCommentToken)).TrimStart('*').Trim();
        return comment.Length > 0 ? comment : null;
    }
}
