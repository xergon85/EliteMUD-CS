namespace EliteMud.Game;

/// <summary>
/// Mob behavior flags from legacy EliteMUD.
/// Source: structs.h lines 373-390
/// These control mob AI behavior in mobile_activity() (mobact.c)
/// </summary>
[Flags]
public enum MobFlags
{
    None = 0,
    
    /// <summary>
    /// MOB_SENTINEL (1 << 1) - Mob stays in place, won't wander.
    /// If displaced, will pathfind back to hometown.
    /// Legacy: mobact.c:195-199
    /// </summary>
    Sentinel = 1 << 1,
    
    /// <summary>
    /// MOB_SCAVENGER (1 << 2) - Picks up valuable objects from room.
    /// Scans room every tick, picks up most valuable item (by cost).
    /// Legacy: mobact.c:145-163
    /// </summary>
    Scavenger = 1 << 2,
    
    /// <summary>
    /// MOB_AGGRESSIVE (1 << 5) - Attacks players on sight.
    /// Triggers when awake and not fighting.
    /// Can be combined with alignment-specific aggro flags.
    /// Legacy: mobact.c:218-242
    /// </summary>
    Aggressive = 1 << 5,
    
    /// <summary>
    /// MOB_STAY_ZONE (1 << 6) - Wandering limited to home zone.
    /// Won't cross zone boundaries during random movement.
    /// Legacy: mobact.c:185-194
    /// </summary>
    StayZone = 1 << 6,
    
    /// <summary>
    /// MOB_WIMPY (1 << 7) - Flees when injured.
    /// If aggressive, won't attack awake targets.
    /// Legacy: mobact.c:226
    /// </summary>
    Wimpy = 1 << 7,
    
    /// <summary>
    /// MOB_AGGRESSIVE_EVIL (1 << 8) - Only attacks evil players.
    /// Requires MOB_AGGRESSIVE to also be set.
    /// Attacks players with alignment <= -350.
    /// Legacy: mobact.c:227-228
    /// </summary>
    AggressiveEvil = 1 << 8,
    
    /// <summary>
    /// MOB_AGGRESSIVE_GOOD (1 << 9) - Only attacks good players.
    /// Requires MOB_AGGRESSIVE to also be set.
    /// Attacks players with alignment >= 350.
    /// Legacy: mobact.c:229-230
    /// </summary>
    AggressiveGood = 1 << 9,
    
    /// <summary>
    /// MOB_AGGRESSIVE_NEUTRAL (1 << 10) - Only attacks neutral players.
    /// Requires MOB_AGGRESSIVE to also be set.
    /// Attacks players with alignment -349 to +349.
    /// Legacy: mobact.c:231-232
    /// </summary>
    AggressiveNeutral = 1 << 10,
    
    /// <summary>
    /// MOB_MEMORY (1 << 11) - Remembers attackers and hunts them down.
    /// Stores player IDs when attacked.
    /// Attacks remembered players on sight or tracks them across rooms.
    /// Legacy: mobact.c:244-270, fight.c:824-827
    /// </summary>
    Memory = 1 << 11,
    
    /// <summary>
    /// MOB_HELPER (1 << 12) - Assists other mobs in combat.
    /// If charmed: helps master's fights.
    /// If independent: helps mobs with similar alignment (within 750 points).
    /// Legacy: fight.c:1575-1600
    /// </summary>
    Helper = 1 << 12
}
