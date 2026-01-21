namespace EliteMud.Game;

/// <summary>
/// Handles HP/Mana/Movement regeneration for players.
/// Based on legacy EliteMUD limits.c point_update() logic.
/// </summary>
public sealed class RegenerationService
{
    // Legacy tick intervals
    private const int GainCheckIntervalSeconds = 6;  // PULSE_GAIN = 6 REAL_SEC
    private const int TickIntervalSeconds = 75;      // SECS_PER_MUD_HOUR = 75
    
    /// <summary>
    /// Calculate gain_count for a standing player over one MUD hour (75 seconds).
    /// Legacy: check_gain() runs every 6 seconds and adds 1 for standing players.
    /// 75 / 6 = 12.5 ticks, so we use 12 as the gain_count.
    /// </summary>
    private const int StandingGainCount = TickIntervalSeconds / GainCheckIntervalSeconds; // 75 / 6 = 12

    /// <summary>
    /// Calculate hit point gain for a player.
    /// Legacy formula: MaxHP * gain_count / (480 - 12 * CON) + CON/2
    /// </summary>
    public static int CalculateHitPointGain(PlayerState player)
    {
        // For now, assume player is standing (gain_count = 12 per MUD hour)
        // TODO: When position system exists, calculate gain_count based on position
        int gainCount = StandingGainCount;
        
        // Legacy formula: gain = GET_MAX_HIT(ch) * ch->specials.gain_count / (480 - 12 * GET_CON(ch)) + GET_CON(ch)/2
        int gain = player.MaxHitPoints * gainCount / (480 - 12 * player.Constitution) + player.Constitution / 2;
        
        // TODO: Add bonuses for AFF_REGENERATION or REGEN room flag when affects system exists
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Calculate mana point gain for a player.
    /// Legacy formula: MaxMana * gain_count / (480 - 12 * WIS) + WIS/2
    /// </summary>
    public static int CalculateManaGain(PlayerState player)
    {
        int gainCount = StandingGainCount;
        
        // Legacy formula: gain = GET_MAX_MANA(ch) * ch->specials.gain_count / (480 - 12 * GET_WIS(ch)) + GET_WIS(ch)/2
        int gain = player.MaxMana * gainCount / (480 - 12 * player.Wisdom) + player.Wisdom / 2;
        
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Calculate movement point gain for a player.
    /// Legacy formula: MaxMove * gain_count / (70 - STR) + STR/2
    /// </summary>
    public static int CalculateMovementGain(PlayerState player)
    {
        int gainCount = StandingGainCount;
        
        // Legacy formula: gain = GET_MAX_MOVE(ch) * ch->specials.gain_count / (70 - GET_STR(ch)) + GET_STR(ch)/2
        int gain = player.MaxMovement * gainCount / (70 - player.Strength) + player.Strength / 2;
        
        // TODO: Reduce gain if poisoned (gain >>= 2) when affects system exists
        // TODO: Reduce gain if hungry/thirsty when hunger/thirst system exists
        
        return Math.Max(0, gain);
    }

    /// <summary>
    /// Apply regeneration to a player's vitals.
    /// This should be called every MUD hour (75 seconds in legacy).
    /// Returns true if any regeneration occurred.
    /// </summary>
    public static bool RegeneratePlayer(PlayerState player)
    {
        // Only regenerate if player is not stunned or worse (position check)
        // For now, always regenerate since we don't have position system yet
        // TODO: Add position check when combat/position system exists
        
        bool anyChange = false;
        
        // Calculate gains
        int hitGain = CalculateHitPointGain(player);
        int manaGain = CalculateManaGain(player);
        int moveGain = CalculateMovementGain(player);
        
        // Apply HP regeneration (capped at max)
        if (player.HitPoints < player.MaxHitPoints)
        {
            player.HitPoints = (short)Math.Min(player.HitPoints + hitGain, player.MaxHitPoints);
            anyChange = true;
        }
        
        // Apply mana regeneration (capped at max)
        if (player.Mana < player.MaxMana)
        {
            player.Mana = (short)Math.Min(player.Mana + manaGain, player.MaxMana);
            anyChange = true;
        }
        
        // Apply movement regeneration (capped at max)
        if (player.Movement < player.MaxMovement)
        {
            player.Movement = (short)Math.Min(player.Movement + moveGain, player.MaxMovement);
            anyChange = true;
        }
        
        return anyChange;
    }
}
