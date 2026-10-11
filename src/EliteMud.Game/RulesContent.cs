namespace EliteMud.Game;

/// <summary>
/// The class, race and progression rules as one snapshot. A rules reload builds a new snapshot and swaps it in
/// whole, so the parts always fit together.
/// </summary>
public sealed class RulesContent
{
    public RulesContent(
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, XpCurveDefinition> xpCurves,
        IReadOnlyDictionary<string, LevelUpProfileDefinition> levelUpProfiles,
        ProgressionRules progression)
    {
        Progression = progression;
        Classes = classes;
        Races = races;
        XpCurves = xpCurves;
        LevelUpProfiles = levelUpProfiles;
        CreationRaces = races.Values
            .Where(race => race.Availability == RaceAvailability.Creation)
            .OrderBy(race => race.MenuKey, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }

    public IReadOnlyDictionary<string, RaceDefinition> Races { get; }

    public IReadOnlyDictionary<string, XpCurveDefinition> XpCurves { get; }

    public IReadOnlyDictionary<string, LevelUpProfileDefinition> LevelUpProfiles { get; }

    public ProgressionRules Progression { get; }

    /// <summary>The races offered at character creation, in menu order.</summary>
    public IReadOnlyList<RaceDefinition> CreationRaces { get; }

    /// <summary>A class's display name, or the id itself when the content doesn't have it.</summary>
    public string ClassName(string classId) => Classes.TryGetValue(classId, out var definition) ? definition.Name : classId;

    /// <summary>A race's display name, or the id itself when the content doesn't have it.</summary>
    public string RaceName(string raceId) => Races.TryGetValue(raceId, out var definition) ? definition.Name : raceId;

    public static RulesContent Empty { get; } = new(
        new Dictionary<string, ClassDefinition>(),
        new Dictionary<string, RaceDefinition>(),
        new Dictionary<string, XpCurveDefinition>(),
        new Dictionary<string, LevelUpProfileDefinition>(),
        new ProgressionRules("return 0", ""));
}
