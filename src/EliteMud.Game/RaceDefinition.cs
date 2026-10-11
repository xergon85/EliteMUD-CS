namespace EliteMud.Game;

/// <summary>
/// A race (content/races/&lt;id&gt;.json). References are resolved when content loads.
/// </summary>
/// <param name="Id">Content id, e.g. "half-elf".</param>
/// <param name="TitleName">Name used in titles, with color codes, e.g. "#cHalf-elven#N".</param>
/// <param name="LegacyNumber">RACE_* number in the legacy code.</param>
/// <param name="MenuKey">The letter a player types to pick it at character creation; only creation races have one.</param>
public sealed record RaceDefinition(
    string Id,
    string Name,
    string TitleName,
    int LegacyNumber,
    RaceAvailability Availability,
    string? AvailabilityNote,
    string? MenuKey,
    StatBlock StatMax,
    StartingPoints Start,
    IReadOnlyList<ClassDefinition> AllowedClasses,
    PhysicalAverages Averages);

/// <summary>How a player gets a race.</summary>
public enum RaceAvailability
{
    /// <summary>Offered at character creation.</summary>
    Creation,

    /// <summary>Given by immortals (after an RP quest, on request).</summary>
    Granted,

    /// <summary>Awarded at a remort.</summary>
    Remort,

    /// <summary>Exists in the data but wasn't used.</summary>
    Unused,

    /// <summary>Not known how players got it.</summary>
    Unknown
}

public sealed record StartingPoints(int HitPoints, int Mana, int Movement);

public sealed record PhysicalAverages(int Age, int HeightInches, int WeightPounds);
