namespace EliteMud.Game;

/// <summary>
/// A character class (content/classes/&lt;id&gt;.json). References are resolved when content loads.
/// </summary>
/// <param name="Id">Content id, e.g. "magic-user".</param>
/// <param name="LegacyNumber">CLASS_* number in the legacy code.</param>
/// <param name="MenuKey">The letter a player types to pick it at character creation (legacy letters).</param>
/// <param name="PrimeStats">Stats that improve more often on level-up (legacy improve_abilities).</param>
/// <param name="StrengthBonus">Gets the legacy strength bonus (str_add) on level-up.</param>
/// <param name="Titles">40 rows, one per 3 levels.</param>
public sealed record ClassDefinition(
    string Id,
    string Name,
    string Abbreviation,
    int LegacyNumber,
    string MenuKey,
    XpCurveDefinition XpCurve,
    LevelUpProfileDefinition LevelUpProfile,
    IReadOnlyList<string> PrimeStats,
    bool StrengthBonus,
    IReadOnlyList<ClassTitle> Titles);

public sealed record ClassTitle(string Male, string Female);
