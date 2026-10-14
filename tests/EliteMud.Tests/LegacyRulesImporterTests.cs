using System.Text.Json;
using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

/// <summary>
/// Runs "elitemud-import rules" on a cut-down legacy source (Fixtures/LegacyRules): two classes, an unused class
/// slot, a dual-class, and four races including a short allowed_classes table, as in the real source.
/// </summary>
public sealed class LegacyRulesImporterTests : IDisposable
{
    private static readonly string LegacyRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyRules");

    private readonly string _content = Path.Combine(Path.GetTempPath(), $"elitemud-rules-import-{Guid.NewGuid():N}");

    public LegacyRulesImporterTests()
    {
        Directory.CreateDirectory(Path.Combine(_content, "meta"));
        File.WriteAllText(Path.Combine(_content, "meta", "legacy-import.json"), """
            {
              "raceAvailability": [
                { "race": "god", "availability": "unused", "note": "not used", "source": "test" },
                { "race": "angel", "availability": "granted", "note": "after an RP quest", "source": "test" },
                { "race": "dwarf", "availability": "granted", "note": "in the menu anyway", "source": "test" },
                { "race": "phoenix", "availability": "granted", "note": "no such race", "source": "test" }
              ]
            }
            """);
    }

    public void Dispose() => Directory.Delete(_content, recursive: true);

    [Fact]
    public async Task Classes_GetCurveProfileStatsAndTitles()
    {
        await ImportAsync();

        var mage = Read("classes/magic-user.json");
        Assert.Equal("Magic-User", mage.GetProperty("name").GetString());
        Assert.Equal("#MMu#N", mage.GetProperty("abbreviation").GetString());
        Assert.Equal(1, mage.GetProperty("legacyNumber").GetInt32());
        Assert.Equal("a", mage.GetProperty("menuKey").GetString()); // 'a' + class number - 1
        Assert.Equal("mage", mage.GetProperty("xpCurve").GetString());
        Assert.Equal("mage", mage.GetProperty("levelUpProfile").GetString());
        Assert.Equal(new[] { "int", "wis" }, Strings(mage.GetProperty("primeStats")));
        Assert.False(mage.GetProperty("strengthBonus").GetBoolean());
        Assert.Equal(4, mage.GetProperty("thac0LevelDivisor").GetInt32());
        Assert.Equal(40, mage.GetProperty("titles").GetArrayLength());
        Assert.Equal("Mage 1ess", mage.GetProperty("titles")[1].GetProperty("female").GetString());

        var warrior = Read("classes/warrior.json");
        Assert.Equal("b", warrior.GetProperty("menuKey").GetString());
        Assert.Equal("fighter", warrior.GetProperty("xpCurve").GetString());
        Assert.Equal(new[] { "str", "con" }, Strings(warrior.GetProperty("primeStats")));
        Assert.True(warrior.GetProperty("strengthBonus").GetBoolean());
        Assert.Equal(3, warrior.GetProperty("thac0LevelDivisor").GetInt32());

        // The DO_NOT_USE slot and the dual-class aren't written
        Assert.Equal(
            new[] { "magic-user.json", "warrior.json" },
            Directory.GetFiles(Path.Combine(_content, "classes")).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task XpCurve_InterpolatesLegacyRowsLikeExpNeeded()
    {
        await ImportAsync();

        // Mage rows are 1, 1000, 2000, ...: level L uses rows[L/3+1] plus a third of the step per L%3 (integer math)
        var thresholds = Read("progression/xp-curves/mage.json").GetProperty("thresholds");
        Assert.Equal(109, thresholds.GetArrayLength());
        Assert.Equal(1333, thresholds[0].GetInt32());
        Assert.Equal(1666, thresholds[1].GetInt32());
        Assert.Equal(2000, thresholds[2].GetInt32());
        Assert.Equal(37333, thresholds[108].GetInt32());
        Assert.Equal(2666, Read("progression/xp-curves/fighter.json").GetProperty("thresholds")[0].GetInt32());
    }

    [Fact]
    public async Task LevelUpProfile_BecomesFormulas()
    {
        await ImportAsync();

        var fighter = Read("progression/level-up/fighter.json");
        Assert.Equal(new[] { "warrior" }, Strings(fighter.GetProperty("classes")));
        Assert.Equal("return math.max(1, random(1, 5) + div(con, 2))", fighter.GetProperty("hpGain").GetString());
        Assert.Equal("if level == 1 then return 0 end return 5", fighter.GetProperty("manaGain").GetString());
        Assert.Equal("return 3", fighter.GetProperty("moveGain").GetString());
        Assert.StartsWith("limits.c:", fighter.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Races_GetTablesAvailabilityAndAllowedClasses()
    {
        await ImportAsync();

        var human = Read("races/human.json");
        Assert.Equal("creation", human.GetProperty("availability").GetString());
        Assert.Equal(new[] { "magic-user", "warrior" }, Strings(human.GetProperty("allowedClasses")));

        var dwarf = Read("races/dwarf.json");
        Assert.Equal("creation", dwarf.GetProperty("availability").GetString()); // the menu beats the config
        Assert.Equal("b", dwarf.GetProperty("menuKey").GetString()); // the letter in the race menu
        Assert.Equal("#RDwarven#N", dwarf.GetProperty("titleName").GetString());
        Assert.Equal(20, dwarf.GetProperty("statMax").GetProperty("str").GetInt32());
        Assert.Equal(16, dwarf.GetProperty("statMax").GetProperty("int").GetInt32());
        Assert.Equal(55, dwarf.GetProperty("start").GetProperty("hp").GetInt32());
        Assert.Equal(48, dwarf.GetProperty("averages").GetProperty("heightInches").GetInt32());
        Assert.Equal(new[] { "warrior" }, Strings(dwarf.GetProperty("allowedClasses")));

        var god = Read("races/god.json");
        Assert.Equal("unused", god.GetProperty("availability").GetString());
        Assert.Equal("not used", god.GetProperty("availabilityNote").GetString());
        Assert.Equal(JsonValueKind.Null, god.GetProperty("menuKey").ValueKind);

        var angel = Read("races/angel.json");
        Assert.Equal("granted", angel.GetProperty("availability").GetString());
        Assert.Equal(0, angel.GetProperty("allowedClasses").GetArrayLength());
    }

    [Fact]
    public async Task Log_RecordsEverySkipAndFix()
    {
        Directory.CreateDirectory(Path.Combine(_content, "classes"));
        File.WriteAllText(Path.Combine(_content, "classes", "old.json"), "{}");

        var log = await ImportAsync();

        AssertLogged(log, LegacyImportSeverity.Warning, "Short race table", "no entry for race 3 (angel)");
        AssertLogged(log, LegacyImportSeverity.Warning, "Race availability", "no race 'phoenix'");
        AssertLogged(log, LegacyImportSeverity.Warning, "Race availability", "dwarf is in the creation menu");
        AssertLogged(log, LegacyImportSeverity.Warning, "Stale file", "remove it if it's obsolete");
        AssertLogged(log, LegacyImportSeverity.Info, "Class skipped", "dual-class: dual/multi-class");
        AssertLogged(log, LegacyImportSeverity.Info, "Class skipped", "unused class slot (Unused UN)");
        AssertLogged(log, LegacyImportSeverity.Info, "Allowed class skipped", "human: DO_NOT_USE");
        AssertLogged(log, LegacyImportSeverity.Info, "Playable races", "2 races in the creation menu");
        Assert.True(File.Exists(Path.Combine(_content, "meta", "legacy-rules-import-log.txt")));
    }

    [Fact]
    public async Task MissingConfig_IsWarnedAndRacesOutsideTheMenuAreUnknown()
    {
        File.Delete(Path.Combine(_content, "meta", "legacy-import.json"));

        var log = await ImportAsync();

        AssertLogged(log, LegacyImportSeverity.Warning, "Config", "not found");
        Assert.Equal("unknown", Read("races/angel.json").GetProperty("availability").GetString());
    }

    private async Task<LegacyImportLog> ImportAsync() =>
        await new LegacyRulesImporter().ImportAsync(LegacyRoot, _content, CancellationToken.None);

    private JsonElement Read(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(_content, relativePath))).RootElement;

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static void AssertLogged(LegacyImportLog log, LegacyImportSeverity severity, string category, string text) =>
        Assert.Contains(log.Messages, m => m.Severity == severity && m.Category == category && m.Text.Contains(text));
}
