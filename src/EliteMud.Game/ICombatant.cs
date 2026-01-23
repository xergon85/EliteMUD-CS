namespace EliteMud.Game;

/// <summary>
/// Common interface for entities that can participate in combat (players and mobs).
/// This includes both attacking and being attacked.
/// Supports both player-vs-player, player-vs-mob, and mob-vs-mob combat.
/// </summary>
public interface ICombatant
{
    /// <summary>
    /// Display name of the combatant (player name or mob short description).
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Current hit points. When this reaches 0 or below, the combatant dies.
    /// </summary>
    short HitPoints { get; set; }
    
    /// <summary>
    /// Maximum hit points for this combatant.
    /// </summary>
    short MaxHitPoints { get; }
    
    /// <summary>
    /// Armor class (AC). Lower is better (harder to hit).
    /// Range: -100 to 100 (higher = worse armor, easier to hit)
    /// </summary>
    short ArmorClass { get; }
    
    /// <summary>
    /// Current position (standing, fighting, sitting, dead, etc.).
    /// Affects ability to attack and damage taken.
    /// </summary>
    Position Position { get; set; }
    
    /// <summary>
    /// Level of the combatant. Affects skill availability and damage calculations.
    /// </summary>
    byte Level { get; }
    
    /// <summary>
    /// Gets the proficiency level (0-100) for a specific skill.
    /// Returns 0 if the combatant doesn't have the skill.
    /// </summary>
    byte GetSkill(SkillType skillType);
    
    /// <summary>
    /// Checks if the combatant has a specific skill (proficiency > 0).
    /// </summary>
    bool HasSkill(SkillType skillType);
    
    // ===== Affects (Buffs/Debuffs) =====
    
    /// <summary>
    /// Get all active affects on this combatant.
    /// </summary>
    IReadOnlyList<Affect> Affects { get; }
    
    /// <summary>
    /// Add an affect to the combatant.
    /// If an affect of the same type already exists, it will be replaced (refreshed).
    /// </summary>
    void AddAffect(Affect affect);
    
    /// <summary>
    /// Remove an affect by type.
    /// Returns true if an affect was removed, false if none existed.
    /// </summary>
    bool RemoveAffect(AffectType type);
    
    /// <summary>
    /// Decrement all affect durations and remove expired ones.
    /// Should be called every PULSE_REGEN (75 seconds).
    /// Returns list of affects that expired.
    /// </summary>
    List<Affect> TickAffects();
    
    /// <summary>
    /// Get effective armor class including all affect modifiers.
    /// Lower is better (negative AC is good).
    /// </summary>
    short GetEffectiveArmorClass();
    
    /// <summary>
    /// Get effective hitroll including all affect modifiers.
    /// Higher is better (bonus to hit).
    /// </summary>
    sbyte GetEffectiveHitroll();
    
    /// <summary>
    /// Get effective damroll including all affect modifiers.
    /// Higher is better (bonus to damage).
    /// </summary>
    sbyte GetEffectiveDamroll();
}
