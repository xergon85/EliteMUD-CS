namespace EliteMud.Legacy.Import;

// Output of LegacyRulesImporter: one JSON file per class, race, XP curve and level-up profile.

internal sealed record ClassRulesContent(
    string Id,
    string Name,
    string Abbreviation,
    int LegacyNumber,
    string MenuKey,
    string XpCurve,
    string LevelUpProfile,
    IReadOnlyList<string> PrimeStats,
    bool StrengthBonus,
    IReadOnlyList<TitleContent> Titles);

internal sealed record TitleContent(string Male, string Female);

internal sealed record RaceRulesContent(
    string Id,
    string Name,
    string TitleName,
    int LegacyNumber,
    string Availability,
    string? AvailabilityNote,
    string? MenuKey,
    StatBlockContent StatMax,
    StartingPointsContent Start,
    IReadOnlyList<string> AllowedClasses,
    AveragesContent Averages);

internal sealed record StatBlockContent(int Str, int Int, int Wis, int Dex, int Con, int Cha);

internal sealed record StartingPointsContent(int Hp, int Mana, int Move);

internal sealed record AveragesContent(int Age, int HeightInches, int WeightPounds);

/// <param name="Thresholds">XP needed to gain each level, index 0 = level 1, up to level 109.</param>
/// <param name="LegacyRows">The legacy titles[][] XP column (one row per 3 levels) the thresholds are generated from.</param>
internal sealed record XpCurveContent(
    string Id,
    IReadOnlyList<string> Classes,
    IReadOnlyList<int> Thresholds,
    IReadOnlyList<int> LegacyRows);

internal sealed record LevelUpProfileContent(
    string Id,
    IReadOnlyList<string> Classes,
    string HpGain,
    string ManaGain,
    string MoveGain,
    string Source);
