using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Bless spell - imbues the target with divine favor and protection.
/// 
/// This is a buff spell that applies TWO affects simultaneously:
/// 1. +2 to hit bonus (APPLY_HITROLL)
/// 2. +5 saving throw vs magic (APPLY_SAVING_MAGIC)
/// Duration: 6 + level hours (MUD hours)
/// 
/// Legacy messages: "You feel righteous." (no room message)
/// 
/// Metadata and formulas loaded from content/spells/spells.json
/// </summary>
public sealed class BlessSpell : ISpellHandler
{
    private readonly SpellMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public BlessSpell(SpellMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySpellType(SpellType.Bless)
            ?? throw new InvalidOperationException("Bless spell metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SpellType SpellType => SpellType.Bless;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int ManaCost => _metadata.ManaCost;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public SpellTargetType TargetType => Enum.Parse<SpellTargetType>(_metadata.TargetType, ignoreCase: true);

    public bool CanCast(ICombatant caster)
    {
        // Must have learned the spell (proficiency > 0)
        if (caster is PlayerState player && player.GetSpell(SpellType.Bless) == 0)
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
        if (caster is PlayerState player && player.GetSpell(SpellType.Bless) == 0)
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
    /// Bless does not deal damage.
    /// </summary>
    public int CalculateDamage(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Bless does not heal.
    /// </summary>
    public int CalculateHealing(ICombatant caster, ICombatant? target = null)
    {
        return 0;
    }

    /// <summary>
    /// Determine if the spell succeeds using Lua formula from spells.json.
    /// Bless typically always succeeds unless the formula specifies otherwise.
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
                spellPercent = caster is PlayerState player ? player.GetSpell(SpellType.Bless) : 100
            }
        );
    }

    /// <summary>
    /// Creates TWO Bless affects to apply to the target:
    /// 1. +2 Hitroll bonus (improves chance to hit)
    /// 2. +5 Saving Magic bonus (improves magic resistance)
    /// 
    /// Duration: 6 + level hours for both affects.
    /// Legacy: "You feel righteous." (to char only, shown once)
    /// </summary>
    public List<Affect> CreateAffects(ICombatant caster, ICombatant target)
    {
        // Calculate duration using Lua formula from spells.json
        // Expected formula: "return 6 + level"
        var duration = _metadata.Mechanics?.DurationFormula != null
            ? _formulaEvaluator.EvaluateInt(
                _metadata.Mechanics.DurationFormula,
                new { level = caster.Level }
            )
            : 6 + caster.Level; // Fallback to legacy default

        // Calculate hitroll bonus using Lua formula from spells.json
        // Expected formula: "return 2"
        var hitrollBonus = _metadata.Mechanics?.HitrollBonusFormula != null
            ? _formulaEvaluator.EvaluateInt(
                _metadata.Mechanics.HitrollBonusFormula,
                new { level = caster.Level }
            )
            : 2; // Fallback to legacy default

        // Calculate saving throw bonus using Lua formula from spells.json
        // Expected formula: "return 5"
        var savingMagicBonus = _metadata.Mechanics?.SavingMagicBonusFormula != null
            ? _formulaEvaluator.EvaluateInt(
                _metadata.Mechanics.SavingMagicBonusFormula,
                new { level = caster.Level }
            )
            : 5; // Fallback to legacy default

        return new List<Affect>
        {
            // First affect: Hitroll bonus
            new Affect
            {
                Type = AffectType.Bless,
                Location = AffectLocation.Hitroll,
                Modifier = hitrollBonus,
                DurationHours = duration,
                Source = "bless",
                ToCharMessage = "You feel righteous.", // Show message only on first affect
                ToRoomMessage = null, // Legacy: no room message for Bless
                WearOffMessage = "You feel less righteous." // Show wear-off only on first affect
            },
            // Second affect: Saving Magic bonus
            new Affect
            {
                Type = AffectType.Bless,
                Location = AffectLocation.SavingMagic,
                Modifier = savingMagicBonus,
                DurationHours = duration,
                Source = "bless",
                ToCharMessage = null, // Don't show message again
                ToRoomMessage = null,
                WearOffMessage = null // Don't show wear-off message again
            }
        };
    }
}
