namespace EliteMud.Game;

/// <summary>
/// Handles combat mechanics: damage calculation, hit/miss, position updates.
/// Based on legacy EliteMUD fight.c logic.
/// </summary>
public static class CombatService
{
    // Position constants (from legacy structs.h)
    public const byte POS_DEAD = 0;
    public const byte POS_MORTALLYW = 1;
    public const byte POS_INCAP = 2;
    public const byte POS_STUNNED = 3;
    public const byte POS_SLEEPING = 4;
    public const byte POS_RESTING = 5;
    public const byte POS_SITTING = 6;
    public const byte POS_FIGHTING = 7;
    public const byte POS_STANDING = 8;

    /// <summary>
    /// Set a player to fighting another player.
    /// Legacy: set_fighting(ch, victim)
    /// </summary>
    public static void SetFighting(PlayerState attacker, int targetConnectionId)
    {
        if (attacker.FightingConnectionId != null)
        {
            throw new InvalidOperationException($"{attacker.Name} is already fighting");
        }

        attacker.FightingConnectionId = targetConnectionId;
        attacker.Position = POS_FIGHTING;
    }

    /// <summary>
    /// Stop a player from fighting.
    /// Legacy: stop_fighting(ch)
    /// </summary>
    public static void StopFighting(PlayerState player)
    {
        player.FightingConnectionId = null;
        if (player.Position == POS_FIGHTING)
        {
            player.Position = POS_STANDING;
        }
    }

    /// <summary>
    /// Roll dice (e.g., 2d6 = 2 dice of 6 sides each).
    /// Legacy: dice(num, size)
    /// </summary>
    public static int RollDice(int number, int size)
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
    /// Legacy uses character level and class.
    /// </summary>
    public static int CalculateBareDamage(PlayerState attacker)
    {
        // Legacy formula varies by class and level
        // Simplified: 1d4 + strength bonus
        int baseDamage = RollDice(1, 4);
        int strBonus = GetStrengthDamageBonus(attacker.Strength);
        return Math.Max(0, baseDamage + strBonus + attacker.Damroll);
    }

    /// <summary>
    /// Get damage bonus from strength.
    /// Legacy: str_app[str].todam
    /// </summary>
    private static int GetStrengthDamageBonus(sbyte strength)
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
    private static int GetStrengthHitBonus(sbyte strength)
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
    public static int CalculateThac0(PlayerState attacker)
    {
        // Simplified THAC0: 20 - level
        // In legacy, warriors have better THAC0 than mages
        int baseThac0 = 20 - attacker.Level;
        return baseThac0;
    }

    /// <summary>
    /// Determine if an attack hits.
    /// Legacy: calculates based on THAC0, AC, dexterity, hitroll
    /// </summary>
    public static bool AttackHits(PlayerState attacker, PlayerState victim)
    {
        int attackerThac0 = CalculateThac0(attacker);
        int attackRoll = RollDice(1, 20);
        
        // Natural 1 always misses, natural 20 always hits
        if (attackRoll == 1) return false;
        if (attackRoll == 20) return true;

        // Calculate hit value: THAC0 - AC - hitroll modifiers
        int hitroll = attacker.Hitroll + GetStrengthHitBonus(attacker.Strength);
        int victimAC = victim.ArmorClass;
        
        // Need to roll: THAC0 - AC or higher (modified by hitroll)
        int neededRoll = attackerThac0 - victimAC - hitroll;
        
        return attackRoll >= neededRoll;
    }

    /// <summary>
    /// Apply damage to a player and update their position.
    /// Legacy: damage() function
    /// </summary>
    public static int ApplyDamage(PlayerState victim, int damage)
    {
        // Cap damage
        damage = Math.Min(damage, 500);
        damage = Math.Max(damage, 0);

        // Apply damage
        victim.HitPoints -= (short)damage;

        // Update position based on HP
        UpdatePosition(victim);

        return damage;
    }

    /// <summary>
    /// Update character position based on current HP.
    /// Legacy: update_pos(ch)
    /// </summary>
    public static void UpdatePosition(PlayerState character)
    {
        if (character.HitPoints > 0)
        {
            // Still conscious
            if (character.Position > POS_STUNNED)
            {
                return; // No change
            }
            character.Position = POS_STANDING;
        }
        else if (character.HitPoints > -3)
        {
            character.Position = POS_STUNNED;
        }
        else if (character.HitPoints > -6)
        {
            character.Position = POS_INCAP;
        }
        else if (character.HitPoints > -11)
        {
            character.Position = POS_MORTALLYW;
        }
        else
        {
            character.Position = POS_DEAD;
        }
    }

    /// <summary>
    /// Perform a full attack from attacker to victim.
    /// Returns damage dealt.
    /// Legacy: hit() function
    /// </summary>
    public static AttackResult PerformAttack(PlayerState attacker, PlayerState victim)
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
        
        // Apply damage
        int actualDamage = ApplyDamage(victim, damage);

        // Consume 1 movement point per attack
        attacker.Movement = (short)Math.Max(0, attacker.Movement - 1);

        return new AttackResult(true, actualDamage, null);
    }

    /// <summary>
    /// Award experience for damage dealt.
    /// Legacy: GET_EXP(ch) += GET_LEVEL(victim) * dam / 2
    /// </summary>
    public static int CalculateExperienceGain(PlayerState victim, int damage)
    {
        return victim.Level * damage / 2;
    }
}

/// <summary>
/// Result of an attack.
/// </summary>
public sealed record AttackResult(bool Hit, int Damage, string? Message);
