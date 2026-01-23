using EliteMud.Game;

namespace EliteMud.Application.Spells;

/// <summary>
/// Result of a spell cast.
/// </summary>
public sealed record SpellCastResult(
    bool Success,
    string? Message,
    int? Damage = null,
    int? Healing = null,
    bool ImprovedSpell = false);

/// <summary>
/// Contract for spell implementations.
/// Spells are player-initiated magical actions that cost mana.
/// 
/// This interface defines domain logic for spells, independent of infrastructure concerns.
/// Spell handlers should be stateless and focus on spell mechanics.
/// 
/// NOTE: Spells are triggered by the 'cast' command.
/// Similar to skills but with mana costs and different target types.
/// </summary>
public interface ISpellHandler
{
    /// <summary>
    /// The spell type this handler implements.
    /// </summary>
    SpellType SpellType { get; }
    
    /// <summary>
    /// Display name of the spell (e.g., "Magic Missile", "Cure Light Wounds").
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description shown in spell lists and help text.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Minimum level required to cast this spell.
    /// Returns 0 if no level requirement.
    /// </summary>
    int MinimumLevel { get; }
    
    /// <summary>
    /// Mana cost to cast this spell.
    /// </summary>
    int ManaCost { get; }
    
    /// <summary>
    /// Number of combat rounds the player must wait after casting this spell.
    /// Legacy: WAIT_STATE value (1 round = 2 seconds)
    /// Returns 0 for no cooldown.
    /// </summary>
    int WaitStateRounds { get; }
    
    /// <summary>
    /// Target type for this spell (self, single enemy, single ally, etc.)
    /// </summary>
    SpellTargetType TargetType { get; }
    
    /// <summary>
    /// Check if the caster can cast this spell right now.
    /// Validates: spell proficiency > 0, mana available, minimum level, etc.
    /// Note: Does NOT check wait state - that's handled by the command layer.
    /// </summary>
    /// <param name="caster">The combatant (player or mob) attempting to cast</param>
    /// <returns>True if the spell can be cast, false otherwise</returns>
    bool CanCast(ICombatant caster);
    
    /// <summary>
    /// Get a failure message explaining why the spell cannot be cast.
    /// Only called if CanCast() returns false.
    /// </summary>
    /// <param name="caster">The combatant (player or mob) attempting to cast</param>
    /// <returns>User-friendly error message</returns>
    string GetCannotCastMessage(ICombatant caster);
    
    /// <summary>
    /// Calculate damage dealt by this spell.
    /// Returns 0 if this spell does not deal damage.
    /// </summary>
    /// <param name="caster">The combatant casting the spell</param>
    /// <param name="target">The target of the spell (optional)</param>
    /// <returns>Damage value (0 or higher)</returns>
    int CalculateDamage(ICombatant caster, ICombatant? target = null);
    
    /// <summary>
    /// Calculate healing provided by this spell.
    /// Returns 0 if this spell does not heal.
    /// </summary>
    /// <param name="caster">The combatant casting the spell</param>
    /// <param name="target">The target of the spell (optional)</param>
    /// <returns>Healing value (0 or higher)</returns>
    int CalculateHealing(ICombatant caster, ICombatant? target = null);
    
    /// <summary>
    /// Determine if the spell casting succeeds.
    /// Uses spell proficiency, level, target armor class, etc.
    /// </summary>
    /// <param name="caster">The combatant casting the spell</param>
    /// <param name="target">The target of the spell (optional)</param>
    /// <returns>True if spell succeeds, false if it fails</returns>
    bool RollSuccess(ICombatant caster, ICombatant? target = null);
    
    /// <summary>
    /// Creates affects to apply to the target (for buff/debuff spells).
    /// Returns empty list if this spell doesn't apply affects.
    /// Some spells (like Bless) may apply multiple affects simultaneously.
    /// </summary>
    /// <param name="caster">The combatant casting the spell</param>
    /// <param name="target">The target of the spell</param>
    /// <returns>List of affects to apply (empty if none)</returns>
    List<Affect> CreateAffects(ICombatant caster, ICombatant target);
}
