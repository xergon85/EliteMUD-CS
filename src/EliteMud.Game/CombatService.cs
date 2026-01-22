namespace EliteMud.Game;

/// <summary>
/// Legacy compatibility wrapper for CombatCalculator.
/// DEPRECATED: Use CombatCalculator for domain logic and CombatMessageFormatter for presentation.
/// 
/// MIGRATION COMPLETE: All codebase usages have been migrated to the new architecture.
/// This class is kept only for backwards compatibility and can be safely removed in a future cleanup.
/// 
/// Message formatting has been moved to EliteMud.Application.Combat.CombatMessageFormatter.
/// Domain logic has been moved to EliteMud.Game.CombatCalculator.
/// </summary>
[Obsolete("Use CombatCalculator for domain logic and CombatMessageFormatter for presentation. Migration complete - this wrapper can be removed.")]
public static class CombatService
{
    // Delegate to CombatCalculator for all calculation methods
    public static void SetFighting(PlayerState attacker, int targetConnectionId)
        => CombatCalculator.SetFighting(attacker, targetConnectionId);

    public static void StopFighting(PlayerState player)
        => CombatCalculator.StopFighting(player);

    public static int RollDice(int number, int size)
        => CombatCalculator.RollDice(number, size);

    public static int CalculateBareDamage(PlayerState attacker)
        => CombatCalculator.CalculateBareDamage(attacker);

    public static int CalculateThac0(PlayerState attacker)
        => CombatCalculator.CalculateThac0(attacker);

    public static bool AttackHits(PlayerState attacker, PlayerState victim)
        => CombatCalculator.AttackHits(attacker, victim);

    public static int CalculateDamageWithPositionMultiplier(int baseDamage, Position victimPosition)
        => CombatCalculator.CalculateDamageWithPositionMultiplier(baseDamage, victimPosition);

    public static int ApplyDamage(PlayerState victim, int damage)
        => CombatCalculator.ApplyDamage(victim, damage);

    public static void UpdatePosition(PlayerState character)
        => CombatCalculator.UpdatePosition(character);

    public static AttackResult PerformAttack(PlayerState attacker, PlayerState victim)
        => CombatCalculator.PerformAttack(attacker, victim);

    public static int CalculateExperienceGain(PlayerState victim, int damage)
        => CombatCalculator.CalculateExperienceGain(victim, damage);

    // Legacy message methods - kept for backwards compatibility
    // NOTE: Message formatting should use EliteMud.Application.Combat.CombatMessageFormatter instead
    public static string? GetDamageFeedbackMessage(PlayerState victim, int damage)
    {
        // "That Really did HURT!" if damage > 20% max HP
        if (damage > victim.MaxHitPoints / 5)
        {
            return "That Really did HURT!";
        }
        
        // Bleeding warning if HP < 20% max HP
        if (victim.HitPoints < victim.MaxHitPoints / 5 && victim.HitPoints > 0)
        {
            return "You wish that your wounds would stop BLEEDING so much!";
        }
        
        return null;
    }

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
        
        // Determine message index based on damage percentage
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

        // Combat damage messages (from legacy fight.c:665-721)
        var damageMessages = new[]
        {
            ("$n misses $N with $s hit.", "#gYou miss $N with your hit.#N", "#G$n misses you with $s hit.#N"),
            ("$n barely hits $N.", "#rYou barely hit $N.#N", "#R$n barely hits you.#N"),
            ("$n scratches $N with $s hit.", "#rYou scratch $N as you hit $M.#N", "#R$n scratches you as $e hits you.#N"),
            ("$n hits $N.", "#rYou hit $N.#N", "#R$n hits you.#N"),
            ("$n hits $N hard.", "#rYou hit $N hard.#N", "#R$n hits you hard.#N"),
            ("$n hits $N very hard.", "#rYou hit $N very hard.#N", "#R$n hits you very hard.#N"),
            ("$n hits $N extremely hard.", "#rYou hit $N extremely hard.#N", "#R$n hits you extremely hard.#N"),
            ("$n massacres $N to small fragments with $s hit.", "#rYou massacre $N to small fragments with your hit.#N", "#R$n massacres you to small fragments with $s hit.#N"),
            ("$n obliterates $N with $s deadly hit!", "#rYou obliterate $N with your deadly hit!#N", "#R$n obliterates you with $s deadly hit!#N"),
            ("$n ANNIHILATES $N with $s wicked hit!!", "#rYou ANNIHILATE $N with your wicked hit!!#N", "#R$n ANNIHILATES you with $s wicked hit!!#N"),
            ("$n ATOMIZES $N with $s cruel hit!!!", "#rYou ATOMIZE $N with your cruel hit!!!#N", "#R$n ATOMIZES you with $s cruel hit!!!#N"),
            ("$n PAINTS THE WALLS WITH $N's head with $s mindblowing hit!!!", "#rYou PAINT THE WALLS with $N's head with your mindblowing hit!!!#N", "#R$n PAINTS THE WALLS with your head with $s mindblowing hit!!!#N")
        };

        // Get the message template based on perspective
        string template = perspective switch
        {
            MessagePerspective.ToChar => damageMessages[msgIndex].Item2,
            MessagePerspective.ToVict => damageMessages[msgIndex].Item3,
            MessagePerspective.ToRoom => damageMessages[msgIndex].Item1,
            _ => throw new ArgumentOutOfRangeException(nameof(perspective))
        };

        // Replace substitution codes
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
/// Result of an attack.
/// </summary>
public sealed record AttackResult(bool Hit, int Damage, string? Message);

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
