using EliteMud.Application.Formulas;
using EliteMud.Application.Skills;
using EliteMud.Application.Spells;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Scripting;

public class FormulaCheckerTests
{
    private readonly FormulaEvaluator _evaluator = new();

    [Fact]
    public void Check_ValidFormula_HasNoErrors()
    {
        var slot = new FormulaSlot("kick.hitFormula", "return ((10 - div(victimAC, 10)) * 2) + random(1,101) <= skillPercent",
            typeof(KickSkill.HitInputs), FormulaResult.Boolean);

        Assert.Empty(FormulaChecker.Check(_evaluator, new[] { slot }));
    }

    [Theory]
    [InlineData("return level + bonus", "unknown variable 'bonus'")]   // variable the slot doesn't provide
    [InlineData("return level > 5", "expected a number")]              // wrong result type
    [InlineData("return level +", "syntax error")]                     // broken Lua
    public void Check_BrokenFormula_ReportsWhy(string formula, string expected)
    {
        var slot = new FormulaSlot("magic missile.damageFormula", formula, typeof(LevelInputs), FormulaResult.Number);

        var error = Assert.Single(FormulaChecker.Check(_evaluator, new[] { slot }));
        Assert.Contains(expected, error);
        Assert.Contains("magic missile.damageFormula", error);
    }

    [Fact]
    public void Check_MissingFormula_IsSkipped()
    {
        // No formula in content means the code uses its fallback; nothing to check
        var slot = new FormulaSlot("bash.damageFormula", null, typeof(NoInputs), FormulaResult.Number);

        Assert.Empty(FormulaChecker.Check(_evaluator, new[] { slot }));
    }

    [Fact]
    public void CreateSample_UsesNonZeroNumbers()
    {
        // 1, not 0, so formulas that divide by an input don't fail the check
        var sample = (DefenseEffectInputs)FormulaChecker.CreateSample(typeof(DefenseEffectInputs));

        Assert.Equal(new DefenseEffectInputs(1, 1), sample);
    }

    /// <summary>Every skill and spell formula in content/ passes the check (the server refuses to start otherwise).</summary>
    [Fact]
    public void Check_AllContentFormulas_AreValid()
    {
        var contentRoot = Path.Combine(RealWorld.RepositoryRoot, "content");
        var skills = new SkillRegistry(new SkillMetadataRegistry(ContentLoader.LoadSkills(contentRoot)), _evaluator);
        var spells = new SpellRegistry(new SpellMetadataRegistry(ContentLoader.LoadSpells(contentRoot)), _evaluator);

        var slots = skills.GetAllActiveSkills().Cast<object>()
            .Concat(skills.GetAllPassiveSkills())
            .Concat(spells.GetAllSpells())
            .OfType<IFormulaSource>()
            .SelectMany(source => source.FormulaSlots)
            .ToList();

        Assert.True(slots.Count >= 29, $"expected all skill and spell formula slots, found {slots.Count}");
        Assert.Empty(FormulaChecker.Check(_evaluator, slots));
    }
}
