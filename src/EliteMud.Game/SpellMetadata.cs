namespace EliteMud.Game;

/// <summary>
/// Metadata for a spell loaded from content/spells/spells.json
/// Contains static spell properties like name, description, class restrictions, and mechanics.
/// </summary>
public sealed record SpellMetadata(
    int Id,
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    string Type,
    string School,
    int MinimumLevel,
    int ManaCost,
    int CastTimeRounds,
    int WaitStateRounds,
    string TargetType,
    IReadOnlyDictionary<string, ClassSpellRestriction> ClassRestrictions,
    SpellMechanics? Mechanics);

/// <summary>
/// Class-specific restrictions for learning and casting a spell
/// </summary>
public sealed record ClassSpellRestriction(
    int? MinLevel,
    int MaxProficiency,
    int Difficulty);

/// <summary>
/// Spell mechanics data (formulas for damage, healing, duration, etc.)
/// Stored as raw JSON-compatible objects for flexibility
/// </summary>
public sealed class SpellMechanics
{
    public string? DamageFormula { get; init; }
    public string? HealingFormula { get; init; }
    public string? SuccessFormula { get; init; }
    public string? DurationFormula { get; init; }
    public string? ArmorClassBonusFormula { get; init; }
    public string? HitrollBonusFormula { get; init; }
    public string? DamrollBonusFormula { get; init; }
    public string? StrengthBonusFormula { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Spell target types
/// </summary>
public enum SpellTargetType
{
    Self,
    SingleEnemy,
    SingleAlly,
    AreaEnemy,
    AreaAlly,
    Room
}
