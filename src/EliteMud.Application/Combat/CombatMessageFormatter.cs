using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// Formats combat messages with damage-based intensity.
/// Based on legacy EliteMUD fight.c message system.
/// </summary>
public static class CombatMessageFormatter
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
    /// Get damage feedback messages for victim.
    /// Legacy: fight.c:972-985
    /// </summary>
    /// <param name="victimMaxHp">Victim's maximum HP</param>
    /// <param name="victimCurrentHp">Victim's current HP</param>
    /// <param name="damage">Damage dealt</param>
    /// <returns>Feedback message, or null if no message</returns>
    public static string? GetDamageFeedbackMessage(int victimMaxHp, int victimCurrentHp, int damage)
    {
        // "That Really did HURT!" if damage > 20% max HP (fight.c:972-973)
        if (damage > victimMaxHp / 5)
        {
            return "That Really did HURT!";
        }
        
        // Bleeding warning if HP < 20% max HP (fight.c:981-985)
        if (victimCurrentHp < victimMaxHp / 5 && victimCurrentHp > 0)
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
