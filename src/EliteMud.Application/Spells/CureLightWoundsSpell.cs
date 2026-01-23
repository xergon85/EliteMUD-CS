using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Cure Light Wounds spell - channels healing energy to restore minor wounds.
/// 
/// This is a basic healing spell that restores hit points to the target.
/// Formula: 1d8 + level/2 healing
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class CureLightWoundsSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public CureLightWoundsSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.CureLightWounds)
            ?? throw new InvalidOperationException("Cure Light Wounds spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.CureLightWounds;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.CureLightWounds) == 0)
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
        if (caster is PlayerState player && player.GetSpell(SpellType.CureLightWounds) == 0)
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
    /// Cure Light Wounds does not deal damage.
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Calculate healing amount using Lua formula from spells.json.
    /// Formula: "return random(1, 8) + math.floor(level / 2)" (1d8 + level/2)
    /// Cure Light Wounds always succeeds (no failure chance).
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        if (_metadata.Mechanics?.HealingFormula == null)
        {
            throw new InvalidOperationException("Cure Light Wounds healing formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateInt(
            _metadata.Mechanics.HealingFormula,
            new { level = caster.Level }
        );
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Formula: "return true" (Cure Light Wounds always succeeds)
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
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.CureLightWounds) : 100
            }
        );
    }
}
