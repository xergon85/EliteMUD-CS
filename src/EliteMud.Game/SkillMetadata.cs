namespace EliteMud.Game;

/// <summary>
/// Metadata for a skill loaded from content/skills/skills.json
/// Contains static skill properties like name, description, class restrictions, and mechanics.
/// </summary>
public sealed record SkillMetadata(
    int Id,
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    string Type,
    string Category,
    int MinimumLevel,
    int WaitStateRounds,
    int SkillgainCooldown,
    IReadOnlyDictionary<string, ClassSkillRestriction> ClassRestrictions,
    SkillMechanics? Mechanics);

/// <summary>
/// Class-specific restrictions for learning and using a skill
/// </summary>
public sealed record ClassSkillRestriction(
    int? MinLevel,
    int MaxProficiency,
    int Difficulty);

/// <summary>
/// Skill mechanics data (formulas, requirements, effects)
/// Stored as raw JSON-compatible objects for flexibility
/// </summary>
public sealed class SkillMechanics
{
    public string? DamageFormula { get; init; }
    public string? DamageMultiplierFormula { get; init; }
    public string? HitFormula { get; init; }
    public string? ActivationFormula { get; init; }
    public string? EffectFormula { get; init; }
    public IReadOnlyList<SkillRequirement>? Requirements { get; init; }
    public IReadOnlyList<SkillEffect>? Effects { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// A requirement for using a skill (position, equipment, victim state, etc.)
/// </summary>
public sealed class SkillRequirement
{
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool Implemented { get; init; } = true;
}

/// <summary>
/// An effect applied when a skill is used (position change, wait state, combat redirection, etc.)
/// </summary>
public sealed class SkillEffect
{
    public string Type { get; init; } = string.Empty;
    public string? Target { get; init; }
    public string? Effect { get; init; }
    public string? Value { get; init; }
    public string? Description { get; init; }
}
