using System.Text.Json;
using EliteMud.Game;

namespace EliteMud.Server;

/// <summary>
/// Loads content/classes, content/races and content/progression into a <see cref="RulesContent"/> snapshot,
/// resolving every id. All problems are collected, so one run lists everything that needs fixing.
/// </summary>
internal static class RulesContentLoader
{
    // Mortals reach level 109 (legacy LEVEL_DEITY - 1); a curve has one value per level
    private const int CurveLevels = 109;
    private const int TitleRows = 40;

    private static readonly string[] StatIds = { "str", "int", "wis", "dex", "con", "cha" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static (RulesContent Rules, IReadOnlyList<string> Errors) Load(string contentRoot)
    {
        var errors = new List<string>();

        var curveFiles = ReadAll<XpCurveFile>(contentRoot, "progression/xp-curves", errors);
        var curveClassLists = curveFiles.ToDictionary(f => f.Data.Id, f => (IReadOnlyList<string>)f.Data.Classes);
        var curves = curveFiles
            .Select(curve =>
            {
                if (curve.Data.Thresholds.Count != CurveLevels)
                {
                    errors.Add($"{curve.Path}: {curve.Data.Thresholds.Count} thresholds, expected one per level 1-{CurveLevels}");
                }

                return new XpCurveDefinition(curve.Data.Id, curve.Data.Thresholds, curve.Data.LegacyRows);
            })
            .ToDictionary(curve => curve.Id);

        var profileFiles = ReadAll<LevelUpProfileFile>(contentRoot, "progression/level-up", errors);
        var profileClassLists = profileFiles.ToDictionary(f => f.Data.Id, f => (IReadOnlyList<string>)f.Data.Classes);
        // The formula check skips blank formulas (skills may leave theirs out); these are required
        foreach (var (path, data) in profileFiles)
        {
            RequireFormula(path, "hpGain", data.HpGain, errors);
            RequireFormula(path, "manaGain", data.ManaGain, errors);
            RequireFormula(path, "moveGain", data.MoveGain, errors);
        }

        var profiles = profileFiles
            .Select(profile => new LevelUpProfileDefinition(
                profile.Data.Id, profile.Data.HpGain, profile.Data.ManaGain, profile.Data.MoveGain, profile.Data.Source ?? ""))
            .ToDictionary(profile => profile.Id);

        var classes = new Dictionary<string, ClassDefinition>(StringComparer.Ordinal);
        foreach (var (path, data) in ReadAll<ClassFile>(contentRoot, "classes", errors))
        {
            var curve = Resolve(curves, data.XpCurve, path, "xpCurve", errors);
            var profile = Resolve(profiles, data.LevelUpProfile, path, "levelUpProfile", errors);
            foreach (var stat in data.PrimeStats.Where(stat => !StatIds.Contains(stat)))
            {
                errors.Add($"{path}: primeStats '{stat}' isn't one of {string.Join(", ", StatIds)}");
            }

            if (data.Titles.Count != TitleRows)
            {
                errors.Add($"{path}: {data.Titles.Count} titles, expected {TitleRows}");
            }

            if (curve is not null && profile is not null)
            {
                classes[data.Id] = new ClassDefinition(
                    data.Id, data.Name, data.Abbreviation, data.LegacyNumber, data.MenuKey, curve, profile, data.PrimeStats,
                    data.StrengthBonus, data.Titles.Select(t => new ClassTitle(t.Male, t.Female)).ToList());
            }
        }

        var races = new Dictionary<string, RaceDefinition>(StringComparer.Ordinal);
        foreach (var (path, data) in ReadAll<RaceFile>(contentRoot, "races", errors))
        {
            var allowed = data.AllowedClasses
                .Select(classId => Resolve(classes, classId, path, "allowedClasses", errors))
                .OfType<ClassDefinition>()
                .ToList();
            // Names only: Enum.TryParse would also accept numbers ("1" = Granted, "999" = undefined)
            var availabilityName = Enum.GetNames<RaceAvailability>()
                .FirstOrDefault(name => string.Equals(name, data.Availability, StringComparison.OrdinalIgnoreCase));
            var availability = availabilityName is null ? default : Enum.Parse<RaceAvailability>(availabilityName);
            if (availabilityName is null)
            {
                errors.Add($"{path}: availability '{data.Availability}' isn't one of {string.Join(", ", Enum.GetNames<RaceAvailability>())}");
            }

            races[data.Id] = new RaceDefinition(
                data.Id,
                data.Name,
                data.TitleName,
                data.LegacyNumber,
                availability,
                data.AvailabilityNote,
                data.MenuKey,
                new StatBlock(data.StatMax.Str, data.StatMax.Dex, data.StatMax.Int, data.StatMax.Wis, data.StatMax.Con, data.StatMax.Cha),
                new StartingPoints(data.Start.Hp, data.Start.Mana, data.Start.Move),
                allowed,
                new PhysicalAverages(data.Averages.Age, data.Averages.HeightInches, data.Averages.WeightPounds));
        }

        CheckMenuKeys("classes", classes.Values.Select(c => (c.Id, (string?)c.MenuKey)), errors);
        CheckMenuKeys("races", races.Values.Where(r => r.Availability == RaceAvailability.Creation)
            .Select(r => (r.Id, r.MenuKey)), errors);
        foreach (var race in races.Values.Where(r => r.Availability != RaceAvailability.Creation && r.MenuKey is not null))
        {
            errors.Add($"races/{race.Id}.json: menuKey '{race.MenuKey}' but availability is {race.Availability}; only creation races are in the menu");
        }

        // Curves and profiles list their classes too (handy when reading them); that list must match the class files
        CheckClassLists(curves.Keys, "progression/xp-curves", id => curveClassLists[id], classes.Values, c => c.XpCurve.Id, errors);
        CheckClassLists(profiles.Keys, "progression/level-up", id => profileClassLists[id], classes.Values, c => c.LevelUpProfile.Id, errors);

        var progression = ReadProgression(contentRoot, curves, errors);

        return (new RulesContent(classes, races, curves, profiles, progression), errors);
    }

    private static void RequireFormula(string path, string field, string formula, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            errors.Add($"{path}: {field} is missing; it's required");
        }
    }

    private static ProgressionRules ReadProgression(
        string contentRoot, IReadOnlyDictionary<string, XpCurveDefinition> curves, List<string> errors)
    {
        const string path = "progression/rules.json";
        // Only returned alongside errors, which stop the server
        var invalid = new ProgressionRules("", new XpCurveDefinition("", new[] { 0 }, Array.Empty<int>()), "");
        var file = Path.Combine(contentRoot, path);
        if (!File.Exists(file))
        {
            errors.Add($"{path}: file not found");
            return invalid;
        }

        try
        {
            var data = JsonSerializer.Deserialize<ProgressionFile>(File.ReadAllText(file), JsonOptions)
                       ?? throw new JsonException("empty file");
            RequireFormula(path, "maxGainFormula", data.MaxGainFormula, errors);
            var mobXpCurve = Resolve(curves, data.MobXpCurve, path, "mobXpCurve", errors);
            return mobXpCurve is null
                ? invalid
                : new ProgressionRules(data.MaxGainFormula, mobXpCurve, data.Source ?? "");
        }
        catch (JsonException ex)
        {
            errors.Add($"{path}: {ex.Message}");
            return invalid;
        }
    }

    /// <summary>
    /// Skills and spells say which classes can learn them (Q1: access is stored on the skill). Every class named
    /// there must exist, or a typo would quietly make the skill unlearnable.
    /// </summary>
    public static IReadOnlyList<string> CheckClassReferences(
        RulesContent rules, IEnumerable<SkillMetadata> skills, IEnumerable<SpellMetadata> spells)
    {
        var known = string.Join(", ", rules.Classes.Keys.Order(StringComparer.Ordinal));
        return skills.Select(skill => (Kind: "skill", skill.Name, Classes: skill.ClassRestrictions.Keys))
            .Concat(spells.Select(spell => (Kind: "spell", spell.Name, Classes: spell.ClassRestrictions.Keys)))
            .SelectMany(entry => entry.Classes
                .Where(classId => !rules.Classes.ContainsKey(classId))
                .Select(classId => $"{entry.Kind} '{entry.Name}': class '{classId}' doesn't exist (known: {known})"))
            .ToList();
    }

    /// <summary>Every menu entry needs one lowercase letter, different from the others in its menu.</summary>
    private static void CheckMenuKeys(string folder, IEnumerable<(string Id, string? MenuKey)> entries, List<string> errors)
    {
        var list = entries.ToList();
        foreach (var (id, key) in list.Where(e => e.MenuKey is not { Length: 1 } || !char.IsAsciiLetterLower(e.MenuKey[0])))
        {
            errors.Add($"{folder}/{id}.json: menuKey '{key}' must be one letter a-z");
        }

        foreach (var clash in list.Where(e => e.MenuKey is not null).GroupBy(e => e.MenuKey).Where(g => g.Count() > 1))
        {
            errors.Add($"{folder}: menuKey '{clash.Key}' is used by {string.Join(", ", clash.Select(e => e.Id).Order(StringComparer.Ordinal))}");
        }
    }

    private static void CheckClassLists(
        IEnumerable<string> ids,
        string folder,
        Func<string, IReadOnlyList<string>> listedClasses,
        IEnumerable<ClassDefinition> classes,
        Func<ClassDefinition, string> usedId,
        List<string> errors)
    {
        foreach (var id in ids)
        {
            var listed = listedClasses(id).Order(StringComparer.Ordinal).ToList();
            var actual = classes.Where(c => usedId(c) == id).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
            if (!listed.SequenceEqual(actual))
            {
                errors.Add($"{folder}/{id}.json: classes lists [{string.Join(", ", listed)}] " +
                           $"but the class files using it are [{string.Join(", ", actual)}]");
            }
        }
    }

    private static T? Resolve<T>(IReadOnlyDictionary<string, T> known, string id, string path, string field, List<string> errors)
        where T : class
    {
        if (known.TryGetValue(id, out var value))
        {
            return value;
        }

        errors.Add($"{path}: {field} '{id}' doesn't exist (known: {string.Join(", ", known.Keys.Order(StringComparer.Ordinal))})");
        return null;
    }

    /// <summary>Every *.json file in a folder; the file name must match the id inside, or the file is skipped.</summary>
    private static List<(string Path, T Data)> ReadAll<T>(string contentRoot, string folder, List<string> errors)
        where T : IdentifiedFile
    {
        var directory = Path.Combine(contentRoot, folder);
        if (!Directory.Exists(directory))
        {
            errors.Add($"{folder}/: folder not found");
            return new List<(string, T)>();
        }

        var result = new List<(string, T)>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var path = $"{folder}/{Path.GetFileName(file)}";
            try
            {
                var data = JsonSerializer.Deserialize<T>(File.ReadAllText(file), JsonOptions)
                           ?? throw new JsonException("empty file");
                // Left out when it doesn't match: file names are unique in a folder, so ids can't clash
                if (data.Id != Path.GetFileNameWithoutExtension(file))
                {
                    errors.Add($"{path}: id '{data.Id}' doesn't match the file name; file skipped");
                    continue;
                }

                result.Add((path, data));
            }
            catch (JsonException ex)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }

        return result;
    }

    // ===== File shapes (written by "elitemud-import rules", edited through the content editor) =====

    private abstract class IdentifiedFile
    {
        public string Id { get; set; } = "";
    }

    private abstract class SharedByClassesFile : IdentifiedFile
    {
        public List<string> Classes { get; set; } = new();
    }

    private sealed class ClassFile : IdentifiedFile
    {
        public string Name { get; set; } = "";
        public string Abbreviation { get; set; } = "";
        public int LegacyNumber { get; set; }
        public string MenuKey { get; set; } = "";
        public string XpCurve { get; set; } = "";
        public string LevelUpProfile { get; set; } = "";
        public List<string> PrimeStats { get; set; } = new();
        public bool StrengthBonus { get; set; }
        public List<TitleFile> Titles { get; set; } = new();
    }

    private sealed class TitleFile
    {
        public string Male { get; set; } = "";
        public string Female { get; set; } = "";
    }

    private sealed class RaceFile : IdentifiedFile
    {
        public string Name { get; set; } = "";
        public string TitleName { get; set; } = "";
        public int LegacyNumber { get; set; }
        public string Availability { get; set; } = "";
        public string? AvailabilityNote { get; set; }
        public string? MenuKey { get; set; }
        public StatsFile StatMax { get; set; } = new();
        public StartFile Start { get; set; } = new();
        public List<string> AllowedClasses { get; set; } = new();
        public AveragesFile Averages { get; set; } = new();
    }

    private sealed class StatsFile
    {
        public int Str { get; set; }
        public int Int { get; set; }
        public int Wis { get; set; }
        public int Dex { get; set; }
        public int Con { get; set; }
        public int Cha { get; set; }
    }

    private sealed class StartFile
    {
        public int Hp { get; set; }
        public int Mana { get; set; }
        public int Move { get; set; }
    }

    private sealed class AveragesFile
    {
        public int Age { get; set; }
        public int HeightInches { get; set; }
        public int WeightPounds { get; set; }
    }

    private sealed class XpCurveFile : SharedByClassesFile
    {
        public List<int> Thresholds { get; set; } = new();
        public List<int> LegacyRows { get; set; } = new();
    }

    private sealed class ProgressionFile
    {
        public string MaxGainFormula { get; set; } = "";
        public string MobXpCurve { get; set; } = "";
        public string? Source { get; set; }
    }

    private sealed class LevelUpProfileFile : SharedByClassesFile
    {
        public string HpGain { get; set; } = "";
        public string ManaGain { get; set; } = "";
        public string MoveGain { get; set; } = "";
        public string? Source { get; set; }
    }
}
