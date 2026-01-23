using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Burning Hands spell - a cone of searing flames erupts from the caster's hands.
/// 
/// This is a fire damage spell that scales with caster level.
/// Formula: 2d6 + level damage
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class BurningHandsSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public BurningHandsSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.BurningHands)
            ?? throw new InvalidOperationException("Burning Hands spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.BurningHands;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.BurningHands) == 0)
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
        if (caster is PlayerState player && player.GetSpell(SpellType.BurningHands) == 0)
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
    /// Calculate burning hands damage using Lua formula from spells.json.
    /// Formula: "return random(1, 6) + random(1, 6) + level" (2d6 + level)
    /// Burning Hands always hits (no miss chance).
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        if (_metadata.Mechanics?.DamageFormula == null)
        {
            throw new InvalidOperationException("Burning Hands damage formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateInt(
            _metadata.Mechanics.DamageFormula,
            new { level = caster.Level }
        );
    }

    /// <summary>
    /// Burning Hands does not heal.
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Formula: "return true" (Burning Hands always hits)
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
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.BurningHands) : 100
            }
        );
    }
}
