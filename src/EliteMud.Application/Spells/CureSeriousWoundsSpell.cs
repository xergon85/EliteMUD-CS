using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Cure Serious Wounds spell - channels greater healing energy to restore moderate wounds.
/// 
/// This is an advanced healing spell that restores more hit points than Cure Light Wounds.
/// Formula: 2d8 + level healing
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class CureSeriousWoundsSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public CureSeriousWoundsSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.CureSeriousWounds)
            ?? throw new InvalidOperationException("Cure Serious Wounds spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.CureSeriousWounds;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.CureSeriousWounds) == 0)
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
        if (caster is PlayerState player && player.GetSpell(SpellType.CureSeriousWounds) == 0)
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
    /// Cure Serious Wounds does not deal damage.
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Calculate healing amount using Lua formula from spells.json.
    /// Formula: "return random(2, 16) + level" (2d8 + level)
    /// Cure Serious Wounds always succeeds (no failure chance).
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        if (_metadata.Mechanics?.HealingFormula == null)
        {
            throw new InvalidOperationException("Cure Serious Wounds healing formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateInt(
            _metadata.Mechanics.HealingFormula,
            new { level = caster.Level }
        );
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Formula: "return true" (Cure Serious Wounds always succeeds)
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
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.CureSeriousWounds) : 100
            }
        );
    }

    /// <summary>
    /// Cure Serious Wounds does not apply affects.
    /// </summary>
    public List<Affect> CreateAffects(ICombatant caster, ICombatant target)
    {
        return new List<Affect>();
    }
}
