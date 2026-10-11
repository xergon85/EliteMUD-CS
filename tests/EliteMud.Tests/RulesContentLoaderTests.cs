using System.Text.Json.Nodes;
using EliteMud.Application.Formulas;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests;

/// <summary>
/// Loading content/classes, races and progression into RulesContent, and the checks that stop the server
/// starting on broken rules content.
/// </summary>
public sealed class RulesContentLoaderTests : IDisposable
{
    private static readonly string RealContent = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly string _copy = Path.Combine(Path.GetTempPath(), $"elitemud-rules-{Guid.NewGuid():N}");

    public RulesContentLoaderTests()
    {
        foreach (var folder in new[] { "classes", "races", "progression/xp-curves", "progression/level-up" })
        {
            Directory.CreateDirectory(Path.Combine(_copy, folder));
            foreach (var file in Directory.GetFiles(Path.Combine(RealContent, folder), "*.json"))
            {
                File.Copy(file, Path.Combine(_copy, folder, Path.GetFileName(file)));
            }
        }
    }

    public void Dispose() => Directory.Delete(_copy, recursive: true);

    [Fact]
    public void RealContent_LoadsWithoutErrors()
    {
        var (rules, errors) = RulesContentLoader.Load(RealContent);

        Assert.Empty(errors);
        Assert.Equal(17, rules.Classes.Count);
        Assert.Equal(25, rules.Races.Count);
        Assert.Equal(6, rules.XpCurves.Count);
        Assert.Equal(4, rules.LevelUpProfiles.Count);
    }

    [Fact]
    public void RealContent_ResolvesReferences()
    {
        var (rules, _) = RulesContentLoader.Load(RealContent);

        var paladin = rules.Classes["paladin"];
        Assert.Equal("fighter", paladin.XpCurve.Id);
        Assert.Equal("priest", paladin.LevelUpProfile.Id);
        Assert.Equal(2666, paladin.XpCurve.XpNeeded(1));

        var dwarf = rules.Races["dwarf"];
        Assert.Equal(RaceAvailability.Creation, dwarf.Availability);
        Assert.Contains(dwarf.AllowedClasses, c => c.Id == "warrior");
        Assert.Equal(RaceAvailability.Remort, rules.Races["avatar"].Availability);
    }

    [Fact]
    public void UnknownCurve_IsReported()
    {
        Edit("classes/paladin.json", json => json["xpCurve"] = "figther");

        var (rules, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("classes/paladin.json") && e.Contains("xpCurve 'figther'"));
        Assert.False(rules.Classes.ContainsKey("paladin"));
    }

    [Fact]
    public void UnknownAllowedClass_IsReported()
    {
        Edit("races/dwarf.json", json => json["allowedClasses"]!.AsArray().Add("warior"));

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("races/dwarf.json") && e.Contains("allowedClasses 'warior'"));
    }

    [Fact]
    public void UnknownAvailability_IsReported()
    {
        Edit("races/dwarf.json", json => json["availability"] = "sometimes");

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("races/dwarf.json") && e.Contains("availability 'sometimes'"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("999")]
    public void NumericAvailability_IsReported(string value)
    {
        Edit("races/dwarf.json", json => json["availability"] = value);

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("races/dwarf.json") && e.Contains($"availability '{value}'"));
    }

    [Fact]
    public void UnknownPrimeStat_IsReported()
    {
        Edit("classes/paladin.json", json => json["primeStats"]!.AsArray().Add("constitution"));

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("classes/paladin.json") && e.Contains("primeStats 'constitution'"));
    }

    [Fact]
    public void IdClashingWithAnotherFile_IsReportedWithoutCrashing()
    {
        Edit("progression/xp-curves/mage.json", json => json["id"] = "fighter");

        var (rules, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("xp-curves/mage.json: id 'fighter'"));
        Assert.Equal("fighter", rules.XpCurves["fighter"].Id);
        Assert.False(rules.XpCurves.ContainsKey("mage"));
    }

    [Fact]
    public void IdNotMatchingFileName_IsReported()
    {
        Edit("races/dwarf.json", json => json["id"] = "dwarves");

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("races/dwarf.json: id 'dwarves'"));
    }

    [Fact]
    public void CurveWithMissingLevels_IsReported()
    {
        Edit("progression/xp-curves/mage.json", json => json["thresholds"]!.AsArray().RemoveAt(108));

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("xp-curves/mage.json: 108 thresholds"));
    }

    [Fact]
    public void ClassListOutOfStep_IsReported()
    {
        // Paladin moves to the mage curve, but the curve files still list it under fighter
        Edit("classes/paladin.json", json => json["xpCurve"] = "mage");

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.Contains("xp-curves/fighter.json") && e.Contains("paladin"));
        Assert.Contains(errors, e => e.Contains("xp-curves/mage.json") && e.Contains("paladin"));
    }

    [Fact]
    public void InvalidJson_IsReported()
    {
        File.WriteAllText(Path.Combine(_copy, "races/dwarf.json"), "{ \"id\": ");

        var (_, errors) = RulesContentLoader.Load(_copy);

        Assert.Contains(errors, e => e.StartsWith("races/dwarf.json: "));
    }

    [Fact]
    public void LevelUpFormulas_PassTheFormulaCheck()
    {
        var (rules, _) = RulesContentLoader.Load(RealContent);
        var slots = rules.LevelUpProfiles.Values.SelectMany(profile => new[]
        {
            new FormulaSlot($"{profile.Id}.hpGain", profile.HpGainFormula, typeof(LevelUpInputs), FormulaResult.Number),
            new FormulaSlot($"{profile.Id}.manaGain", profile.ManaGainFormula, typeof(LevelUpInputs), FormulaResult.Number),
            new FormulaSlot($"{profile.Id}.moveGain", profile.MoveGainFormula, typeof(LevelUpInputs), FormulaResult.Number)
        });

        Assert.Empty(FormulaChecker.Check(new FormulaEvaluator(), slots));
    }

    [Fact]
    public void LevelUpFormula_WithUnknownVariable_IsRejected()
    {
        var slot = new FormulaSlot(
            "priest.hpGain", "return random(1, 5) + div(constitution, 4)", typeof(LevelUpInputs), FormulaResult.Number);

        var errors = FormulaChecker.Check(new FormulaEvaluator(), new[] { slot });

        Assert.Contains(errors, e => e.Contains("constitution"));
    }

    [Fact]
    public void ZoneMobs_KeepTheirExperience()
    {
        // zones/zone_005: bouncy ferret, "Experience": 666
        Assert.Equal(666, RealWorld.State.MobDefinitions[500].Experience);
    }

    private void Edit(string relativePath, Action<JsonObject> change)
    {
        var path = Path.Combine(_copy, relativePath);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(json);
        File.WriteAllText(path, json.ToJsonString());
    }
}
