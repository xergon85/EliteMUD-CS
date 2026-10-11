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

    /// <summary>
    /// Every "...Formula" field in the content files reaches a formula slot. A field the loader forgets to map
    /// would otherwise silently fall back to hard-coded values and skip the startup check.
    /// </summary>
    // Content formulas nothing reads yet; each entry points to the issue that wires it up.
    private static readonly HashSet<string> KnownUnusedFormulas = new(StringComparer.OrdinalIgnoreCase)
    {
        // #33: multi-attack is hard-coded in GameTickService (difficulty 120/140/160)
        "second attack.activationFormula",
        "third attack.activationFormula",
        "fourth attack.activationFormula",
        "dual wield.activationFormula",
    };

    [Fact]
    public void ContentFormulaFields_AllReachTheirSlots()
    {
        var contentRoot = Path.Combine(RealWorld.RepositoryRoot, "content");
        var slots = AllSlots(contentRoot)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Formula))
            .Select(slot => slot.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var (file, key) in new[] { ("skills/skills.json", "skills"), ("spells/spells.json", "spells") })
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(contentRoot, file)));
            foreach (var entry in document.RootElement.GetProperty(key).EnumerateArray())
            {
                if (!entry.TryGetProperty("mechanics", out var mechanics) || mechanics.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                var name = entry.GetProperty("name").GetString();
                missing.AddRange(mechanics.EnumerateObject()
                    .Where(field => field.Name.EndsWith("Formula", StringComparison.Ordinal)
                                    && field.Value.ValueKind == System.Text.Json.JsonValueKind.String
                                    && !field.Value.GetString()!.StartsWith("NOT_YET", StringComparison.Ordinal))
                    .Select(field => $"{name}.{field.Name}")
                    .Where(slotName => !slots.Contains(slotName) && !KnownUnusedFormulas.Contains(slotName)));
            }
        }

        Assert.True(missing.Count == 0, "Content formulas that never reach a slot:\n" + string.Join("\n", missing));
    }

    /// <summary>Every skill and spell formula in content/ passes the check (the server refuses to start otherwise).</summary>
    [Fact]
    public void Check_AllContentFormulas_AreValid()
    {
        var slots = AllSlots(Path.Combine(RealWorld.RepositoryRoot, "content")).ToList();

        Assert.True(slots.Count >= 29, $"expected all skill and spell formula slots, found {slots.Count}");
        Assert.Empty(FormulaChecker.Check(_evaluator, slots));
    }

    private List<FormulaSlot> AllSlots(string contentRoot)
    {
        var skills = new SkillRegistry(new SkillMetadataRegistry(ContentLoader.LoadSkills(contentRoot)), _evaluator);
        var spells = new SpellRegistry(new SpellMetadataRegistry(ContentLoader.LoadSpells(contentRoot)), _evaluator);

        return skills.GetAllActiveSkills().Cast<object>()
            .Concat(skills.GetAllPassiveSkills())
            .Concat(spells.GetAllSpells())
            .OfType<IFormulaSource>()
            .SelectMany(source => source.FormulaSlots)
            .ToList();
    }
}
