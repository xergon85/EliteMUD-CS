namespace EliteMud.Game;

/// <summary>
/// Handles HP/Mana/Movement regeneration for players.
/// Based on legacy EliteMUD limits.c point_update() logic.
/// </summary>
public sealed class RegenerationService
{
    /// <summary>
    /// Increment gain_count based on player's position.
    /// This should be called every PULSE_GAIN (every few seconds).
    /// Legacy: check_gain() in comm.c - runs every game tick and increments gain_count by position.
    /// </summary>
    public static void IncrementGainCount(PlayerState player)
    {
        // Legacy logic from comm.c:1795-1808
        // Sleeping: +4, Resting: +3, Sitting: +2, Standing: +1, Fighting: +0
        player.GainCount += player.Position switch
        {
            Position.Sleeping => 4,
            Position.Resting => 3,
            Position.Sitting => 2,
            Position.Standing => 1,
            _ => 0  // Fighting, Stunned, Dead, etc. = no regen accumulation
        };
    }

    /// <summary>
    /// Calculate hit point gain for a player.
    /// Legacy formula: MaxHP * gain_count / (480 - 12 * CON) + CON/2
    /// </summary>
    public static int CalculateHitPointGain(PlayerState player, int effectiveMaxHP)
    {
        // Legacy formula: gain = GET_MAX_HIT(ch) * ch->specials.gain_count / (480 - 12 * GET_CON(ch)) + GET_CON(ch)/2
        int divisor = 480 - 12 * player.Constitution;
        if (divisor <= 0) divisor = 1;  // Prevent divide by zero for very high CON
        
        int gain = effectiveMaxHP * player.GainCount / divisor + player.Constitution / 2;
        
        // TODO: Add bonuses for AFF_REGENERATION or REGEN room flag when affects system exists
        // if (IS_AFFECTED(ch, AFF_REGENERATION) || ROOM_FLAGGED(IN_ROOM(ch), REGEN))
        //   gain += gain/2;
        
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Calculate mana point gain for a player.
    /// Legacy formula: MaxMana * gain_count / (480 - 12 * WIS) + WIS/2
    /// </summary>
    public static int CalculateManaGain(PlayerState player, int effectiveMaxMana)
    {
        // Legacy formula: gain = GET_MAX_MANA(ch) * ch->specials.gain_count / (480 - 12 * GET_WIS(ch)) + GET_WIS(ch)/2
        int divisor = 480 - 12 * player.Wisdom;
        if (divisor <= 0) divisor = 1;  // Prevent divide by zero for very high WIS
        
        int gain = effectiveMaxMana * player.GainCount / divisor + player.Wisdom / 2;
        
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Calculate movement point gain for a player.
    /// Legacy formula: MaxMove * gain_count / (70 - STR) + STR/2
    /// </summary>
    public static int CalculateMovementGain(PlayerState player, int effectiveMaxMove)
    {
        // Legacy formula: gain = GET_MAX_MOVE(ch) * ch->specials.gain_count / (70 - GET_STR(ch)) + GET_STR(ch)/2
        int divisor = 70 - player.Strength;
        if (divisor <= 0) divisor = 1;  // Prevent divide by zero for very high STR
        
        int gain = effectiveMaxMove * player.GainCount / divisor + player.Strength / 2;
        
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Apply regeneration to a player's vitals.
    /// This should be called every PULSE_REGEN (every 60 seconds recommended).
    /// After regeneration, resets gain_count to 0.
    /// Returns true if any regeneration occurred.
    /// </summary>
    public static bool RegeneratePlayer(PlayerState player, int effectiveMaxHP, int effectiveMaxMana, int effectiveMaxMove)
    {
        // Only regenerate if player is stunned or better (not dead/incap/mortally wounded)
        // Legacy: if (GET_POS(i) >= POS_STUNNED)
        if (player.Position < Position.Stunned)
        {
            return false;  // Too wounded to regenerate
        }
        
        bool anyChange = false;
        
        // Calculate gains using accumulated gain_count
        int hitGain = CalculateHitPointGain(player, effectiveMaxHP);
        int manaGain = CalculateManaGain(player, effectiveMaxMana);
        int moveGain = CalculateMovementGain(player, effectiveMaxMove);
        
        // Apply HP regeneration (capped at effective max)
        if (player.HitPoints < effectiveMaxHP)
        {
            player.HitPoints = (short)Math.Min(player.HitPoints + hitGain, effectiveMaxHP);
            anyChange = true;
        }
        
        // Apply mana regeneration (capped at effective max)
        if (player.Mana < effectiveMaxMana)
        {
            player.Mana = (short)Math.Min(player.Mana + manaGain, effectiveMaxMana);
            anyChange = true;
        }
        
        // Apply movement regeneration (capped at effective max)
        if (player.Movement < effectiveMaxMove)
        {
            player.Movement = (short)Math.Min(player.Movement + moveGain, effectiveMaxMove);
            anyChange = true;
        }
        
        // Reset gain_count after regeneration
        // Legacy: i->specials.gain_count = 0 in point_update()
        player.GainCount = 0;
        
        return anyChange;
    }
}
