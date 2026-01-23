using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Armor spell - surrounds the target with a protective magical barrier.
/// 
/// This is a defensive buff spell that reduces armor class (lower AC = better defense).
/// Provides -20 AC bonus for 24 + level hours (MUD hours).
/// Legacy messages: "You feel someone protecting you." (no room message)
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class ArmorSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public ArmorSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.Armor)
            ?? throw new InvalidOperationException("Armor spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.Armor;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.Armor) == 0)
        {
            return false;
        }

        // Must be at minimum level
        if (caster.Level < MinimumLevel)
        {
            return false;
        }

        // Must have enough mana
        if (caster is PlayerState playerState && playerState.Mana < ManaCost)
        {
            return false;
        }

        // Must be standing or fighting (not sitting/resting/sleeping/incapacitated)
        if (caster.Position < Position.Fighting)
        {
            return false;
        }

        return true;
    }

    public string GetCannotCastMessage(ICombatant caster)
    {
        if (caster is PlayerState player && player.GetSpell(SpellType.Armor) == 0)
        {
            return "You don't know that spell!";
        }

        if (caster.Level < MinimumLevel)
        {
            return $"You must be at least level {MinimumLevel} to cast this spell.";
        }

        if (caster is PlayerState playerState && playerState.Mana < ManaCost)
        {
            return $"You don't have enough mana. ({ManaCost} required, {playerState.Mana} available)";
        }

        if (caster.Position < Position.Fighting)
        {
            return "You can't concentrate while sitting down!";
        }

        return "You can't cast that spell right now.";
    }

    /// <summary>
    /// Armor does not deal damage.
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Armor does not heal.
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Armor typically always succeeds unless the formula specifies otherwise.
    /// </summary>
    public bool RollSuccess(ICombatant caster, ICombatant? target = null)
    {
        if (_metadata.Mechanics?.SuccessFormula == null)
        {
            return true; // Default: always succeed if no formula
        }

        return _formulaEvaluator.EvaluateBool(
            _metadata.Mechanics.SuccessFormula,
            new
            {
                level = caster.Level,
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.Armor) : 100
            }
        );
    }

    /// <summary>
    /// Creates the Armor affect to apply to the target.
    /// Provides -20 AC (lower is better) for 24 + level hours.
    /// Legacy: "You feel someone protecting you." (to char only)
    /// </summary>
    public List<Affect> CreateAffects(ICombatant caster, ICombatant target)
    {
        // Calculate duration using Lua formula from spells.json
        // Expected formula: "return 24 + level"
        var duration = _metadata.Mechanics?.DurationFormula != null
            ? _formulaEvaluator.EvaluateInt(
                _metadata.Mechanics.DurationFormula,
                new { level = caster.Level }
            )
            : 24 + caster.Level; // Fallback to legacy default

        // Calculate AC modifier using Lua formula from spells.json
        // Expected formula: "return -20"
        var modifier = _metadata.Mechanics?.ArmorClassBonusFormula != null
            ? _formulaEvaluator.EvaluateInt(
                _metadata.Mechanics.ArmorClassBonusFormula,
                new { level = caster.Level }
            )
            : -20; // Fallback to legacy default

        return new List<Affect>
        {
            new Affect
            {
                Type = AffectType.Armor,
                Location = AffectLocation.ArmorClass,
                Modifier = modifier,
                DurationHours = duration,
                Source = "armor",
                ToCharMessage = "You feel someone protecting you.",
                ToRoomMessage = null, // Legacy: no room message for Armor
                WearOffMessage = "You feel less protected."
            }
        };
    }
}
