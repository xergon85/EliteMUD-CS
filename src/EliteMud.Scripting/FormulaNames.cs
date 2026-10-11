using System.Text.RegularExpressions;

namespace EliteMud.Scripting;

/// <summary>
/// Finds the variable names a formula reads, without running it, so a name in a branch that a sample run
/// doesn't take is still checked (e.g. "return level == 1 and 1 or typo").
/// </summary>
public static partial class FormulaNames
{
    private static readonly HashSet<string> LuaKeywords = new(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in",
        "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while"
    };

    /// <summary>Names the formula reads that aren't in <paramref name="allowed"/> and that it doesn't define itself.</summary>
    public static IReadOnlyList<string> FindUnknown(string formula, IReadOnlySet<string> allowed)
    {
        var code = StripStringsAndComments(formula);
        var defined = DefinedNames(code);

        return Identifier().Matches(code)
            .Select(match => match.Value)
            .Where(name => !LuaKeywords.Contains(name) && !allowed.Contains(name) && !defined.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Names a formula introduces: locals, function parameters, loop variables and assignment targets.</summary>
    private static HashSet<string> DefinedNames(string code)
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Definitions().Matches(code))
        {
            foreach (var name in match.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                defined.Add(name);
            }
        }

        return defined;
    }

    private static string StripStringsAndComments(string formula)
    {
        var code = LongComment().Replace(formula, " ");
        code = LineComment().Replace(code, " ");
        code = LongString().Replace(code, " \"\" ");
        return QuotedString().Replace(code, " \"\" ");
    }

    // A name that isn't a field access (math.floor, obj:method); \b keeps "1e5" from matching "e5"
    [GeneratedRegex(@"(?<![.:]\s*)\b[A-Za-z_]\w*\b")]
    private static partial Regex Identifier();

    // local a, b | function name(a, b) | function(a, b) | for i = | for k, v in | name = (not ==)
    [GeneratedRegex(@"\blocal\s+(?:function\s+)?(?<names>[A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)"
                    + @"|\bfunction\b[^(]*\((?<names>[^)]*)\)"
                    + @"|\bfor\s+(?<names>[A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*(?:=|\bin\b)"
                    + @"|(?<![.:]\s*)(?<names>\b[A-Za-z_]\w*)\s*=(?!=)")]
    private static partial Regex Definitions();

    [GeneratedRegex(@"--\[(=*)\[.*?\]\1\]", RegexOptions.Singleline)]
    private static partial Regex LongComment();

    [GeneratedRegex(@"--[^\n]*")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\[(=*)\[.*?\]\1\]", RegexOptions.Singleline)]
    private static partial Regex LongString();

    [GeneratedRegex(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'")]
    private static partial Regex QuotedString();
}
