using EliteMud.Scripting;

namespace EliteMud.Tests.Scripting;

public class FormulaNamesTests
{
    private static readonly IReadOnlySet<string> Allowed =
        new HashSet<string> { "level", "spellPercent", "math", "random", "div" };

    [Theory]
    [InlineData("return level == 1 and 1 or typo", "typo")]          // only in the branch a sample run skips
    [InlineData("return div(levle, 2)", "levle")]
    [InlineData("if level > 10 then return bonus end return 1", "bonus")]
    public void FindUnknown_ReportsNamesInEveryBranch(string formula, string expected)
    {
        Assert.Equal(new[] { expected }, FormulaNames.FindUnknown(formula, Allowed));
    }

    [Theory]
    [InlineData("return math.max(1, math.floor(level / 2))")]          // field access after "."
    [InlineData("return random(1, 101) <= spellPercent")]
    [InlineData("local bonus = div(level, 3) return bonus + 1")]       // local defined in the formula
    [InlineData("for i = 1, 3 do level = level + i end return level")]  // loop variable, assignment
    [InlineData("return level -- typo in a comment is fine")]
    [InlineData("local note = 'typo inside a string' return level")]
    [InlineData("return level >= 10 and 2e3 or 1")]                    // keywords, comparison, number with exponent
    public void FindUnknown_AcceptsValidFormulas(string formula)
    {
        Assert.Empty(FormulaNames.FindUnknown(formula, Allowed));
    }
}
