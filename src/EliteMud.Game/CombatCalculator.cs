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
        public const int Track = PULSE_VIOLENCE * 1;     // 1 round (2 seconds)
    }
    
    /// <summary>
    /// Skillgain cooldown in seconds.
    /// Minimum time between skill improvements for the same skill.
    /// 
    /// Legacy: Used ch->specials.skillgain accumulator (0-100) that incremented each tick.
    /// EliteMUD-CS: Simplified to timestamp-based cooldown for predictability.
    /// </summary>
    public const int SkillgainCooldownSeconds = 60;
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
    private readonly IPassiveSkillHandler _parrySkill;
    private readonly Random _random;

    /// <summary>
    /// Constructor for dependency injection.
    /// </summary>
    /// <param name="dodgeSkill">Dodge skill handler (injected from SkillRegistry)</param>
    /// <param name="parrySkill">Parry skill handler (injected from SkillRegistry)</param>
    /// <param name="random">Dice for hit and damage rolls; tests pass fixed ones (default: shared)</param>
    public CombatCalculator(IPassiveSkillHandler dodgeSkill, IPassiveSkillHandler parrySkill, Random? random = null)
    {
        _dodgeSkill = dodgeSkill;
        _parrySkill = parrySkill;
        _random = random ?? Random.Shared;
    }
    /// <summary>
    /// Start fighting an opponent (legacy set_fighting). A player gets up and takes the fighting position; a mob
    /// keeps its position, as in legacy.
    /// </summary>
    public void SetFighting(ICombatant combatant, ICombatant opponent)
    {
        if (combatant.Fighting != null)
        {
            throw new InvalidOperationException($"{combatant.Name} is already fighting");
        }

        combatant.Fighting = opponent;
        if (combatant is PlayerState)
        {
            combatant.Position = Position.Fighting;
        }
    }

    /// <summary>
    /// Stop fighting (legacy stop_fighting): anyone conscious stands up, e.g. someone bashed to the ground. Unlike
    /// legacy, the stunned, incapacitated and dying stay down (legacy stands them up too, with no update_pos after).
    /// </summary>
    public static void StopFighting(ICombatant combatant)
    {
        combatant.Fighting = null;
        if (combatant.Position > Position.Stunned)
        {
            combatant.Position = Position.Standing;
        }
    }

    /// <summary>
    /// Stop everyone among <paramref name="others"/> who is fighting <paramref name="opponent"/>, e.g. when the
    /// opponent flees or leaves the game (legacy extract_char stops everyone fighting the one who leaves).
    /// </summary>
    public static void StopEveryoneFighting(ICombatant opponent, IEnumerable<ICombatant> others)
    {
        foreach (var combatant in others.Where(other => other.Fighting == opponent).ToList())
        {
            StopFighting(combatant);
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
            total += _random.Next(1, size + 1);
        }
        return total;
    }

    /// <summary>
    /// Apply weapon special effects (blessed, evil, flaming).
    /// Legacy: fight.c:1461-1468
    /// BLESS: +3d6 damage vs evil targets
    /// EVIL: +3d6 damage vs good targets
    /// FLAME: +3d6 fire damage (should check save, but always applies for now)
    /// </summary>
    /// <param name="weaponFlags">The weapon's flags (e.g., "Bless", "Evil", "Flame")</param>
    /// <param name="victim">The target combatant</param>
    /// <returns>Bonus damage from weapon effects</returns>
    public int ApplyWeaponEffects(IReadOnlyList<string> weaponFlags, ICombatant victim)
    {
        int bonusDamage = 0;
        
        // BLESS weapon: +3d6 vs evil
        // Legacy: if (IS_OBJ_STAT(wielded, ITEM_BLESS) && IS_EVIL(victim)) dam += dice(3,6);
        if (weaponFlags.Contains("Bless") && victim.IsEvil())
        {
            bonusDamage += RollDice(3, 6);
        }
        
        // EVIL weapon: +3d6 vs good
        // Legacy: if (IS_OBJ_STAT(wielded, ITEM_EVIL) && IS_GOOD(victim)) dam += dice(3,6);
        if (weaponFlags.Contains("Evil") && victim.IsGood())
        {
            bonusDamage += RollDice(3, 6);
        }
        
        // FLAME weapon: +3d6 fire damage (victim can save to negate)
        // Legacy: if (IS_OBJ_STAT(wielded, ITEM_FLAME) && !saves_spell(victim, SAVING_PHYSICAL, NULL, SAVE_NEGATE))
        if (weaponFlags.Contains("Flame"))
        {
            // Victim gets a physical saving throw to avoid fire damage
            if (!MakesSavingThrow(victim, SavingThrowType.Physical))
            {
                bonusDamage += RollDice(3, 6);
            }
        }
        
        return bonusDamage;
    }
    
    /// <summary>
    /// Check if a combatant makes a saving throw.
    /// Legacy: saves_spell(victim, save_type, caster, save_modifier)
    /// 
    /// Saving throw system:
    /// - Base save determined by level (improves every 3 levels)
    /// - Equipment bonuses from affects (SavingPhysical, etc.)
    /// - Roll d20, success if roll >= target number
    /// - Lower save values are better (easier to make the save)
    /// 
    /// Save modifiers:
    /// - SAVE_NEGATE (0): Normal save
    /// - SAVE_HALF (1): Half damage on failed save (not used here)
    /// </summary>
    /// <param name="victim">The combatant making the save</param>
    /// <param name="saveType">Type of saving throw</param>
    /// <returns>True if the save succeeds, false if it fails</returns>
    public bool MakesSavingThrow(ICombatant victim, SavingThrowType saveType)
    {
        // Base saving throw by level (starts at 16, improves by 1 every 3 levels)
        // Legacy used class-based saving throw tables, this is simplified
        // Lower is better: level 1 = 16, level 3 = 15, level 6 = 14, etc.
        int baseSave = 16 - (victim.Level / 3);
        baseSave = Math.Max(1, baseSave); // Minimum save of 1
        
        // Apply equipment/affect bonuses
        // Saving throw affects are negative (e.g., -5 to save = 5 bonus to roll)
        int saveBonus = 0;
        foreach (var affect in victim.Affects)
        {
            if (saveType == SavingThrowType.Physical && affect.Location == AffectLocation.SavingPhysical)
            {
                saveBonus -= affect.Modifier; // Negative modifier = bonus
            }
            else if (saveType == SavingThrowType.Mental && affect.Location == AffectLocation.SavingMental)
            {
                saveBonus -= affect.Modifier;
            }
            else if (saveType == SavingThrowType.Magic && affect.Location == AffectLocation.SavingMagic)
            {
                saveBonus -= affect.Modifier;
            }
            else if (saveType == SavingThrowType.Poison && affect.Location == AffectLocation.SavingPoison)
            {
                saveBonus -= affect.Modifier;
            }
        }
        
        // Roll d20
        int roll = RollDice(1, 20);
        
        // Success if roll + bonus >= base save threshold
        // Example: base save 10, roll 12, bonus 2 → 12+2=14 >= 10 → success
        return (roll + saveBonus) >= baseSave;
    }
    
    /// <summary>
    /// The melee to-hit roll (legacy hit(), fight.c:1380–1420): a d20 against the attacker's THAC0 less their to-hit
    /// bonus, and the victim's armor class less their dexterity bonus, divided by 10. A victim who isn't awake
    /// (sleeping or worse) is always hit; one out of movement is easier to hit. A natural 1 misses and a natural 20
    /// hits. The caller passes effective numbers (base + spells + equipment).
    /// </summary>
    /// <param name="attackerThac0">THAC0: 20 for a mob, the class's for a player; lower hits more often</param>
    /// <param name="attackerToHit">Strength's to-hit bonus + hitroll + (int − 13) / 3 + (wis − 13) / 3</param>
    public bool AttackHits(
        int attackerThac0,
        int attackerToHit,
        int victimArmorClass,
        int victimDexterity,
        bool victimAwake,
        bool victimOutOfMovement)
    {
        int calcThac0 = attackerThac0 - attackerToHit;
        int diceRoll = RollDice(1, 20);

        // Legacy takes the dexterity bonus only when awake; a victim who isn't awake is always hit anyway
        int victimAc = victimArmorClass - victimDexterity * 10 / 6;

        if (victimOutOfMovement)
        {
            victimAc += 50;
        }

        victimAc = Math.Max(-200, victimAc / 10);

        // Legacy: MISS if (diceroll < 20 && AWAKE(victim)) && (diceroll == 1 || calc_thaco - diceroll > victim_ac)
        var miss = diceRoll < 20 && victimAwake && (diceRoll == 1 || calcThac0 - diceRoll > victimAc);
        return !miss;
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
            // Integer division, as the C code computes it (its comments say x1.33, x1.66 and so on):
            // sitting and resting x1; sleeping, stunned, incapacitated and mortally wounded x2
            int multiplier = (3 + (int)Position.Fighting - (int)victimPosition) / 3;
            baseDamage *= multiplier;
        }
        
        return baseDamage;
    }

    /// <summary>
    /// The victim's side of a melee hit (legacy hit(), before damage()): dodge or parry. The damage comes from the
    /// melee roll, the position multiplier included. Changes no hit points; the result is the damage to deal. A
    /// player or a mob: a mob dodges and parries with its skills like anyone (decision 1 of #42; mobs have none yet,
    /// #72).
    /// </summary>
    public DamageResult DefendAgainstMelee(ICombatant victim, int damage)
    {
        // Check for passive defensive skills (dodge, then parry)
        // Legacy: fight.c:1543-1551 (dodge), fight.c:1523-1535 (parry)
        var dodgeResult = _dodgeSkill.TryActivate(victim, damage);
        bool dodged = dodgeResult.Activated;
        string? defenseMessage = dodgeResult.Message;
        
        if (dodged)
        {
            damage = dodgeResult.ModifiedValue;
            
            // Improve skill on successful dodge
            (victim as PlayerState)?.TryImproveSkill(SkillType.Dodge);
        }
        else
        {
            // Try parry if dodge failed
            var parryResult = _parrySkill.TryActivate(victim, damage);
            bool parried = parryResult.Activated;
            
            if (parried)
            {
                damage = parryResult.ModifiedValue;
                defenseMessage = parryResult.Message;
                
                // Improve skill on successful parry
                (victim as PlayerState)?.TryImproveSkill(SkillType.Parry);
            }
        }
        
        // Cap damage
        damage = Math.Min(damage, 500);
        damage = Math.Max(damage, 0);

        return new DamageResult(damage, dodged, defenseMessage);
    }

    /// <summary>
    /// Update character position based on current HP.
    /// Legacy: update_pos(ch) from fight.c:160
    /// 
    /// Auto-stand behavior:
    /// - If HP > 0 and position is Sleeping/Resting/Sitting → force to Standing
    /// - If HP > 0 and position is Fighting/Standing → keep current position
    /// - If HP <= 0 → set to dying/dead positions
    /// 
    /// This prevents the bug where victims get stuck sitting/sleeping while fighting.
    /// </summary>
    public void UpdatePosition(PlayerState character)
    {
        if (character.HitPoints > 0)
        {
            // Still conscious - check if we need to auto-stand
            // Legacy: if (GET_HIT(victim) > 0 && GET_POS(victim) > POS_STUNNED) return;
            //         else if (GET_HIT(victim) > 0) GET_POS(victim) = POS_STANDING;
            
            if (character.Position > Position.Stunned && character.Position < Position.Fighting)
            {
                // Sleeping/Resting/Sitting → auto-stand when taking damage
                character.Position = Position.Standing;
            }
            else if (character.Position >= Position.Fighting)
            {
                // Already Fighting or Standing → keep current position
                return;
            }
            else
            {
                // Stunned or worse → stand up
                character.Position = Position.Standing;
            }
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
    /// Calculate alignment shift from killing a combatant.
    /// Legacy: change_alignment() in fight.c:445-460
    /// 
    /// Formula: "If you kill a monster with alignment A, you move 1/16th of the way
    /// to having alignment -A."
    /// 
    /// shift = (-victim_alignment - killer_alignment) >> 4
    /// 
    /// This creates a convergent system where:
    /// - Killing evil creatures shifts you toward good
    /// - Killing good creatures shifts you toward evil
    /// - The closer you are to the target alignment, the smaller the shift
    /// - No distinction between PvE and PvP
    /// - No level-based multipliers
    /// 
    /// Examples:
    /// - Neutral (0) kills evil (-500): shift = (500 - 0) / 16 = +31
    /// - Neutral (0) kills very evil (-1000): shift = (1000 - 0) / 16 = +62
    /// - Good (500) kills evil (-500): shift = (500 - 500) / 16 = 0 (already at target)
    /// </summary>
    /// <param name="killer">The combatant performing the kill</param>
    /// <param name="victim">The combatant being killed</param>
    /// <param name="isPvP">True if this is player-vs-player combat (unused in legacy)</param>
    /// <returns>Alignment change (positive = toward good, negative = toward evil)</returns>
    public int CalculateAlignmentShift(ICombatant killer, ICombatant victim, bool isPvP)
    {
        // Legacy formula: GET_ALIGNMENT(ch) += (-GET_ALIGNMENT(victim) - GET_ALIGNMENT(ch)) >> 4;
        // The >> 4 is a right shift by 4 bits, equivalent to division by 16
        // In C, right shift on signed integers is implementation-defined for negative numbers,
        // but typically performs arithmetic shift (sign extension), effectively dividing by 16 and rounding toward zero
        
        int targetAlignment = -victim.Alignment;
        int currentAlignment = killer.Alignment;
        int difference = targetAlignment - currentAlignment;
        
        // Divide by 16, matching C's behavior (rounds toward zero for both positive and negative)
        int shift = difference / 16;
        
        return shift;
    }
    
    /// <summary>
    /// Apply alignment shift to a player, clamping to valid range.
    /// Alignment range: -1000 (pure evil) to +1000 (pure good).
    /// </summary>
    /// <param name="player">The player whose alignment is changing</param>
    /// <param name="shift">The alignment change (positive = good, negative = evil)</param>
    public void ApplyAlignmentShift(PlayerState player, int shift)
    {
        player.Alignment = (short)Math.Clamp(player.Alignment + shift, -1000, 1000);
    }
}

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
