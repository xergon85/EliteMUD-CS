using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Lightning Bolt spell - a powerful stroke of lightning arcs toward the enemy.
/// 
/// This is a high-damage electricity spell that scales significantly with caster level.
/// Formula: level to level*6 damage
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class LightningBoltSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public LightningBoltSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.LightningBolt)
            ?? throw new InvalidOperationException("Lightning Bolt spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.LightningBolt;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.LightningBolt) == 0)
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
        if (caster is PlayerState player && player.GetSpell(SpellType.LightningBolt) == 0)
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
    /// Calculate lightning bolt damage using Lua formula from spells.json.
    /// Formula: "return random(level, level * 6)"
    /// High variance damage (level 10 = 10-60, level 20 = 20-120).
    /// Lightning Bolt always hits (no miss chance).
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        if (_metadata.Mechanics?.DamageFormula == null)
        {
            throw new InvalidOperationException("Lightning Bolt damage formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateInt(
            _metadata.Mechanics.DamageFormula,
            new { level = caster.Level }
        );
    }

    /// <summary>
    /// Lightning Bolt does not heal.
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Formula: "return true" (Lightning Bolt always hits)
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
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.LightningBolt) : 100
            }
        );
    }

    /// <summary>
    /// Lightning Bolt does not apply affects.
    /// </summary>
    public List<Affect> CreateAffects(ICombatant caster, ICombatant target)
    {
        return new List<Affect>();
    }
}
