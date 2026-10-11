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
        IReadOnlyDictionary<string, LevelUpProfileDefinition> levelUpProfiles)
    {
        Classes = classes;
        Races = races;
        XpCurves = xpCurves;
        LevelUpProfiles = levelUpProfiles;
    }

    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }

    public IReadOnlyDictionary<string, RaceDefinition> Races { get; }

    public IReadOnlyDictionary<string, XpCurveDefinition> XpCurves { get; }

    public IReadOnlyDictionary<string, LevelUpProfileDefinition> LevelUpProfiles { get; }

    public static RulesContent Empty { get; } = new(
        new Dictionary<string, ClassDefinition>(),
        new Dictionary<string, RaceDefinition>(),
        new Dictionary<string, XpCurveDefinition>(),
        new Dictionary<string, LevelUpProfileDefinition>());
}
