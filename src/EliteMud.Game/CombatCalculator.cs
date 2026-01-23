namespace EliteMud.Game;

/// <summary>
/// Combat timing constants.
/// Legacy: PULSE_VIOLENCE and related wait state values from structs.h
/// </summary>
public static class CombatConstants
{
    /// <summary>
    /// One combat round (PULSE_VIOLENCE).
    /// Skills apply wait states in multiples of this value.
    /// </summary>
    public const int PULSE_VIOLENCE = 1;
    
    /// <summary>
    /// Wait state constants for common actions.
    /// Legacy values from act.offensive.c:
    /// - Kick: WAIT_STATE(ch, PULSE_VIOLENCE * 3) = 3 rounds
    /// - Bash: WAIT_STATE(ch, PULSE_VIOLENCE * 2) = 2 rounds
    /// - Most attacks: PULSE_VIOLENCE * 2-3
    /// </summary>
    public static class WaitStates
    {
        public const int Kick = PULSE_VIOLENCE * 3;      // 3 rounds (6 seconds)
        public const int Bash = PULSE_VIOLENCE * 2;      // 2 rounds (4 seconds)
        public const int Rescue = PULSE_VIOLENCE * 2;    // 2 rounds (4 seconds)
        public const int Backstab = PULSE_VIOLENCE * 2;  // 2 rounds (4 seconds)
        public const int Flee = PULSE_VIOLENCE * 1;      // 1 round (2 seconds)
    }
}

/// <summary>
/// Pure domain service for combat calculations.
/// Contains stateless combat logic: damage, hit/miss, position updates.
/// Based on legacy EliteMUD fight.c logic.
/// 
/// Note: This class is now instance-based to support dependency injection of SkillRegistry.
/// However, it remains stateless - all methods are pure functions with no mutable state.
/// </summary>
public class CombatCalculator
{
    private readonly IPassiveSkillHandler _dodgeSkill;

    /// <summary>
    /// Constructor for dependency injection.
    /// </summary>
    /// <param name="dodgeSkill">Dodge skill handler (injected from SkillRegistry)</param>
    public CombatCalculator(IPassiveSkillHandler dodgeSkill)
    {
        _dodgeSkill = dodgeSkill;
    }
    /// <summary>
    /// Set a player to fighting another player.
    /// Auto-stands the player if they are sitting/resting/sleeping.
    /// Legacy: set_fighting(ch, victim)
    /// </summary>
    public void SetFighting(PlayerState attacker, int targetConnectionId)
    {
        if (attacker.FightingConnectionId != null)
        {
            throw new InvalidOperationException($"{attacker.Name} is already fighting");
        }

        // Auto-stand if player is sitting/resting/sleeping
        // Legacy does this implicitly by setting position to Fighting
        // but we'll be explicit about the transition
        if (attacker.Position < Position.Standing)
        {
            attacker.Position = Position.Standing;
        }

        attacker.FightingConnectionId = targetConnectionId;
        attacker.Position = Position.Fighting;
    }

    /// <summary>
    /// Stop a player from fighting.
    /// Legacy: stop_fighting(ch)
    /// </summary>
    public void StopFighting(PlayerState player)
    {
        player.FightingConnectionId = null;
        if (player.Position == Position.Fighting)
        {
            player.Position = Position.Standing;
        }
    }

    /// <summary>
    /// Roll dice (e.g., 2d6 = 2 dice of 6 sides each).
    /// Legacy: dice(num, size)
    /// </summary>
    public int RollDice(int number, int size)
    {
        if (number <= 0 || size <= 0) return 0;
        
        int total = 0;
        for (int i = 0; i < number; i++)
        {
            total += Random.Shared.Next(1, size + 1);
        }
        return total;
    }

    /// <summary>
    /// Calculate base damage for an unarmed attack.
    /// Legacy: fight.c:1439-1458
    /// Formula: str_todam + damroll + random(0,2)
    /// </summary>
    public int CalculateBareDamage(PlayerState attacker)
    {
        // Legacy: dam = str_todam + damroll + number(0, 2) for bare hands
        int strBonus = GetStrengthDamageBonus(attacker.Strength);
        int baseDamage = Random.Shared.Next(0, 3); // 0-2 damage (legacy: number(0, 2))
        return Math.Max(0, strBonus + attacker.Damroll + baseDamage);
    }

    /// <summary>
    /// Get damage bonus from strength.
    /// Legacy: str_app[str].todam
    /// </summary>
    private int GetStrengthDamageBonus(sbyte strength)
    {
        // Simplified strength to-damage table
        if (strength <= 5) return -2;
        if (strength <= 10) return -1;
        if (strength <= 15) return 0;
        if (strength <= 17) return 1;
        if (strength == 18) return 2;
        return 3; // 19+
    }

    /// <summary>
    /// Get to-hit bonus from strength.
    /// Legacy: str_app[str].tohit
    /// </summary>
    private int GetStrengthHitBonus(sbyte strength)
    {
        // Simplified strength to-hit table
        if (strength <= 5) return -2;
        if (strength <= 10) return -1;
        if (strength <= 15) return 0;
        if (strength <= 17) return 1;
        if (strength == 18) return 2;
        return 3; // 19+
    }

    /// <summary>
    /// Calculate THAC0 (To Hit Armor Class 0) for a player.
    /// Legacy uses class and level tables.
    /// Lower is better (easier to hit).
    /// </summary>
    public int CalculateThac0(PlayerState attacker)
    {
        // Simplified THAC0: 20 - level
        // In legacy, warriors have better THAC0 than mages
        int baseThac0 = 20 - attacker.Level;
        return baseThac0;
    }

    /// <summary>
    /// Determine if an attack hits.
    /// Legacy: calculates based on THAC0, AC, dexterity, hitroll (fight.c:1380-1418)
    /// </summary>
    public bool AttackHits(PlayerState attacker, PlayerState victim)
    {
        // Calculate base THAC0
        int calcThac0 = CalculateThac0(attacker);
        
        // Apply bonuses (subtract from THAC0 - lower is better)
        // Legacy: calc_thaco -= (str_tohit + hitroll + int_bonus + wis_bonus + skillbonus - drunk)
        int strBonus = GetStrengthHitBonus(attacker.Strength);
        int intBonus = (attacker.Intelligence - 13) / 3;
        int wisBonus = (attacker.Wisdom - 13) / 3;
        
        calcThac0 -= (strBonus + attacker.Hitroll + intBonus + wisBonus);
        
        // Roll d20
        int diceRoll = RollDice(1, 20);
        
        // Natural 1 always misses, natural 20 always hits
        if (diceRoll == 1) return false;
        if (diceRoll == 20) return true;

        // Calculate victim AC (legacy: fight.c:1395-1415)
        int victimAc = victim.ArmorClass;
        
        // Dexterity bonus (if awake)
        // Legacy: victim_ac -= (GET_DEX(victim)) * 10 / 6
        victimAc -= (victim.Dexterity * 10) / 6;
        
        // Divide by 10 (legacy: victim_ac = MAX(-200, victim_ac / 10))
        victimAc = Math.Max(-200, victimAc / 10);
        
        // Legacy hit formula: MISS if (calc_thaco - diceroll) > victim_ac
        // Inverted: HIT if diceroll >= (calc_thaco - victim_ac)
        return diceRoll >= (calcThac0 - victimAc);
    }

    /// <summary>
    /// Calculate damage with position multipliers for helpless victims.
    /// Legacy: fight.c:1482-1489
    /// </summary>
    /// <param name="baseDamage">Base damage before multipliers</param>
    /// <param name="victimPosition">Victim's current position</param>
    /// <returns>Damage after position multiplier</returns>
    public int CalculateDamageWithPositionMultiplier(int baseDamage, Position victimPosition)
    {
        // If victim is not in fighting position, apply damage multiplier
        // Legacy formula: dam *= (3 + POS_FIGHTING - GET_POS(victim)) / 3
        if (victimPosition < Position.Fighting)
        {
            // Calculate multiplier: (3 + 7 - position) / 3
            // POS_SITTING (6): 1.33x
            // POS_RESTING (5): 1.66x
            // POS_SLEEPING (4): 2.00x
            // POS_STUNNED (3): 2.33x
            // POS_INCAP (2): 2.66x
            // POS_MORTALLYW (1): 3.00x
            int multiplierNumerator = 3 + (int)Position.Fighting - (int)victimPosition;
            baseDamage = baseDamage * multiplierNumerator / 3;
        }
        
        return baseDamage;
    }

    /// <summary>
    /// Apply damage to a player and update their position.
    /// Legacy: damage() function
    /// </summary>
    public DamageResult ApplyDamage(PlayerState victim, int damage)
    {
        // Apply position multiplier before capping damage
        damage = CalculateDamageWithPositionMultiplier(damage, victim.Position);
        
        // Check for passive defensive skills (dodge)
        // Legacy: fight.c:1543-1551
        var dodgeResult = _dodgeSkill.TryActivate(victim, damage);
        bool dodged = dodgeResult.Activated;
        if (dodged)
        {
            damage = dodgeResult.ModifiedValue;
            
            // Improve skill on successful dodge
            victim.TryImproveSkill(SkillType.Dodge);
        }
        
        // Cap damage
        damage = Math.Min(damage, 500);
        damage = Math.Max(damage, 0);

        // Apply damage
        victim.HitPoints -= (short)damage;

        // Update position based on HP
        UpdatePosition(victim);

        return new DamageResult(damage, dodged, dodgeResult.Message);
    }

    /// <summary>
    /// Update character position based on current HP.
    /// Legacy: update_pos(ch)
    /// </summary>
    public void UpdatePosition(PlayerState character)
    {
        if (character.HitPoints > 0)
        {
            // Still conscious
            if (character.Position > Position.Stunned)
            {
                return; // No change
            }
            character.Position = Position.Standing;
        }
        else if (character.HitPoints > -3)
        {
            character.Position = Position.Stunned;
        }
        else if (character.HitPoints > -6)
        {
            character.Position = Position.Incapacitated;
        }
        else if (character.HitPoints > -11)
        {
            character.Position = Position.MortallyWounded;
        }
        else
        {
            character.Position = Position.Dead;
        }
    }

    /// <summary>
    /// Perform a full attack from attacker to victim.
    /// Returns damage dealt.
    /// Legacy: hit() function
    /// </summary>
    public AttackResult PerformAttack(PlayerState attacker, PlayerState victim)
    {
        // Check if out of moves (too tired to fight)
        if (attacker.Movement < 1)
        {
            return new AttackResult(false, 0, "You are too exhausted to attack!");
        }

        // Check if attack hits
        bool hits = AttackHits(attacker, victim);
        if (!hits)
        {
            return new AttackResult(false, 0, "You miss!");
        }

        // Calculate damage
        int damage = CalculateBareDamage(attacker);
        
        // Apply damage (includes passive defensive skills)
        var damageResult = ApplyDamage(victim, damage);

        // Consume 1 movement point per attack
        attacker.Movement = (short)Math.Max(0, attacker.Movement - 1);

        return new AttackResult(true, damageResult.Damage, damageResult.Message);
    }

    /// <summary>
    /// Award experience for damage dealt.
    /// Legacy: GET_EXP(ch) += GET_LEVEL(victim) * dam / 2
    /// </summary>
    public int CalculateExperienceGain(PlayerState victim, int damage)
    {
        return victim.Level * damage / 2;
    }
}

/// <summary>
/// Result of an attack.
/// </summary>
public sealed record AttackResult(bool Hit, int Damage, string? Message);

/// <summary>
/// Result of applying damage (includes dodge check).
/// </summary>
public sealed record DamageResult(int Damage, bool Dodged, string? Message);

/// <summary>
/// Message perspective for combat messages.
/// Based on legacy TO_CHAR, TO_VICT, TO_ROOM flags.
/// </summary>
public enum MessagePerspective
{
    /// <summary>Send to the attacker</summary>
    ToChar,
    /// <summary>Send to the victim</summary>
    ToVict,
    /// <summary>Send to everyone else in the room</summary>
    ToRoom
}
