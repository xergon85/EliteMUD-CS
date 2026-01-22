namespace EliteMud.Game;

/// <summary>
/// Handles combat mechanics: damage calculation, hit/miss, position updates.
/// Based on legacy EliteMUD fight.c logic.
/// </summary>
public static class CombatService
{
    // Attack type text (from legacy fight.c:49)
    private static readonly (string Singular, string Plural)[] AttackHitText = new[]
    {
        ("hit", "hits"),
        ("pound", "pounds"),
        ("pierce", "pierces"),
        ("slash", "slashes"),
        ("blast", "blasts"),
        ("whip", "whips"),
        ("pierce", "pierces"),
        ("claw", "claws"),
        ("bite", "bites"),
        ("sting", "stings"),
        ("crush", "crushes")
    };

    // Combat damage messages (from legacy fight.c:665-721)
    // Based on percentage of victim's max HP
    // Color codes: #g=bright green (attacker miss), #G=dark green (victim miss),
    //              #r=bright red (attacker hit), #R=dark red (victim hit), #N=normal
    // Legacy used �1G (dark green), �1g (bright green), �1R (dark red), �1r (bright red)
    private static readonly (string ToRoom, string ToChar, string ToVict)[] DamageMessages = new[]
    {
        // 0: Miss (0 damage) - bright green for attacker, dark green for victim
        ("$n misses $N with $s hit.", 
         "#gYou miss $N with your hit.#N", 
         "#G$n misses you with $s hit.#N"),
        
        // 1: < 1% of max HP - bright red for attacker, dark red for victim
        ("$n barely hits $N.", 
         "#rYou barely hit $N.#N", 
         "#R$n barely hits you.#N"),
        
        // 2: 1-2% of max HP
        ("$n scratches $N with $s hit.", 
         "#rYou scratch $N as you hit $M.#N", 
         "#R$n scratches you as $e hits you.#N"),
        
        // 3: 2-3% of max HP
        ("$n hits $N.", 
         "#rYou hit $N.#N", 
         "#R$n hits you.#N"),
        
        // 4: 3-5% of max HP
        ("$n hits $N hard.", 
         "#rYou hit $N hard.#N", 
         "#R$n hits you hard.#N"),
        
        // 5: 5-8% of max HP
        ("$n hits $N very hard.", 
         "#rYou hit $N very hard.#N", 
         "#R$n hits you very hard.#N"),
        
        // 6: 8-13% of max HP
        ("$n hits $N extremely hard.", 
         "#rYou hit $N extremely hard.#N", 
         "#R$n hits you extremely hard.#N"),
        
        // 7: 13-21% of max HP
        ("$n massacres $N to small fragments with $s hit.", 
         "#rYou massacre $N to small fragments with your hit.#N", 
         "#R$n massacres you to small fragments with $s hit.#N"),
        
        // 8: 21-34% of max HP
        ("$n obliterates $N with $s deadly hit!", 
         "#rYou obliterate $N with your deadly hit!#N", 
         "#R$n obliterates you with $s deadly hit!#N"),
        
        // 9: 34-55% of max HP
        ("$n ANNIHILATES $N with $s wicked hit!!", 
         "#rYou ANNIHILATE $N with your wicked hit!!#N", 
         "#R$n ANNIHILATES you with $s wicked hit!!#N"),
        
        // 10: 55-89% of max HP
        ("$n ATOMIZES $N with $s cruel hit!!!", 
         "#rYou ATOMIZE $N with your cruel hit!!!#N", 
         "#R$n ATOMIZES you with $s cruel hit!!!#N"),
        
        // 11: >= 89% of max HP
        ("$n PAINTS THE WALLS WITH $N's head with $s mindblowing hit!!!", 
         "#rYou PAINT THE WALLS with $N's head with your mindblowing hit!!!#N", 
         "#R$n PAINTS THE WALLS with your head with $s mindblowing hit!!!#N")
    };

    /// <summary>
    /// Set a player to fighting another player.
    /// Auto-stands the player if they are sitting/resting/sleeping.
    /// Legacy: set_fighting(ch, victim)
    /// </summary>
    public static void SetFighting(PlayerState attacker, int targetConnectionId)
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
    public static void StopFighting(PlayerState player)
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
    /// Legacy: fight.c:1439-1458
    /// Formula: str_todam + damroll + random(0,2)
    /// </summary>
    public static int CalculateBareDamage(PlayerState attacker)
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
    /// Legacy: calculates based on THAC0, AC, dexterity, hitroll (fight.c:1380-1418)
    /// </summary>
    public static bool AttackHits(PlayerState attacker, PlayerState victim)
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
    public static int CalculateDamageWithPositionMultiplier(int baseDamage, Position victimPosition)
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
    public static int ApplyDamage(PlayerState victim, int damage)
    {
        // Apply position multiplier before capping damage
        damage = CalculateDamageWithPositionMultiplier(damage, victim.Position);
        
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

    /// <summary>
    /// Get damage feedback messages for victim.
    /// Legacy: fight.c:972-985
    /// </summary>
    /// <param name="victim">The character taking damage</param>
    /// <param name="damage">Damage dealt</param>
    /// <returns>Feedback message, or null if no message</returns>
    public static string? GetDamageFeedbackMessage(PlayerState victim, int damage)
    {
        // "That Really did HURT!" if damage > 20% max HP (fight.c:972-973)
        if (damage > victim.MaxHitPoints / 5)
        {
            return "That Really did HURT!";
        }
        
        // Bleeding warning if HP < 20% max HP (fight.c:981-985)
        if (victim.HitPoints < victim.MaxHitPoints / 5 && victim.HitPoints > 0)
        {
            return "You wish that your wounds would stop BLEEDING so much!";
        }
        
        return null;
    }

    /// <summary>
    /// Format a combat message based on damage dealt as percentage of victim's max HP.
    /// Legacy: dam_message() in fight.c:658-777
    /// </summary>
    /// <param name="attackerName">Name of the attacker</param>
    /// <param name="victimName">Name of the victim</param>
    /// <param name="damage">Damage dealt</param>
    /// <param name="victimMaxHp">Victim's maximum HP</param>
    /// <param name="perspective">Message perspective (ToChar, ToVict, ToRoom)</param>
    /// <returns>Formatted combat message</returns>
    public static string FormatCombatMessage(
        string attackerName, 
        string victimName, 
        int damage, 
        int victimMaxHp,
        MessagePerspective perspective)
    {
        // Trim names to remove legacy leading/trailing whitespace and newlines
        attackerName = attackerName.Trim();
        victimName = victimName.Trim();
        
        // Calculate damage as percentage of victim's max HP
        int percent = damage * 100 / Math.Max(1, victimMaxHp);
        
        // Determine message index based on damage percentage (legacy logic from fight.c:729-743)
        int msgIndex;
        if (damage == 0)        msgIndex = 0;   // Miss
        else if (percent < 1)   msgIndex = 1;   // Barely
        else if (percent < 2)   msgIndex = 2;   // Scratch
        else if (percent < 3)   msgIndex = 3;   // Normal hit
        else if (percent < 5)   msgIndex = 4;   // Hard
        else if (percent < 8)   msgIndex = 5;   // Very hard
        else if (percent < 13)  msgIndex = 6;   // Extremely hard
        else if (percent < 21)  msgIndex = 7;   // Massacre
        else if (percent < 34)  msgIndex = 8;   // Obliterate
        else if (percent < 55)  msgIndex = 9;   // Annihilate
        else if (percent < 89)  msgIndex = 10;  // Atomize
        else                    msgIndex = 11;  // Paint the walls

        // Get the message template based on perspective
        string template = perspective switch
        {
            MessagePerspective.ToChar => DamageMessages[msgIndex].ToChar,
            MessagePerspective.ToVict => DamageMessages[msgIndex].ToVict,
            MessagePerspective.ToRoom => DamageMessages[msgIndex].ToRoom,
            _ => throw new ArgumentOutOfRangeException(nameof(perspective))
        };

        // Replace substitution codes (legacy act() function from comm.c)
        // $n = attacker's name (or "you" for ToChar)
        // $N = victim's name (or "you" for ToVict)
        // $e = he/she/it (attacker)
        // $E = he/she/it (victim)
        // $M = him/her/it (victim)
        // $s = his/her/its (attacker)
        string message = template;
        
        if (perspective == MessagePerspective.ToChar)
        {
            message = message.Replace("$n", "You");
            message = message.Replace("$N", victimName);
            message = message.Replace("$M", "them");
            message = message.Replace("$s", "your");
            message = message.Replace("$e", "you");
        }
        else if (perspective == MessagePerspective.ToVict)
        {
            message = message.Replace("$n", attackerName);
            message = message.Replace("$N", "you");
            message = message.Replace("$M", "you");
            message = message.Replace("$s", "their");
            message = message.Replace("$e", "they");
        }
        else // ToRoom
        {
            message = message.Replace("$n", attackerName);
            message = message.Replace("$N", victimName);
            message = message.Replace("$M", "them");
            message = message.Replace("$s", "their");
            message = message.Replace("$e", "they");
        }

        return message;
    }
}

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

/// <summary>
/// Result of an attack.
/// </summary>
public sealed record AttackResult(bool Hit, int Damage, string? Message);
