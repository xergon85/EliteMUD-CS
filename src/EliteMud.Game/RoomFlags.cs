namespace EliteMud.Game;

/// <summary>
/// Room flags that modify room behavior.
/// Legacy: structs.h room_flags bit definitions
/// Bit positions must match legacy for data compatibility.
/// </summary>
[Flags]
public enum RoomFlags
{
    None = 0,
    
    /// <summary>
    /// Can't see without light source.
    /// Legacy: DARK (1 << 0)
    /// </summary>
    Dark = 1 << 0,
    
    /// <summary>
    /// Death trap - instant kill on entry.
    /// Legacy: DEATH (1 << 1)
    /// </summary>
    Death = 1 << 1,
    
    /// <summary>
    /// Mobs won't wander into this room.
    /// Legacy: NO_MOB (1 << 2)
    /// </summary>
    NoMob = 1 << 2,
    
    /// <summary>
    /// Indoors - no weather messages.
    /// Legacy: INDOORS (1 << 3)
    /// </summary>
    Indoors = 1 << 3,
    
    /// <summary>
    /// Safe zone - no stealing, aggressive mobs, or summoning.
    /// Note: Aggressive mobs DON'T check this flag (only memory mobs do).
    /// Legacy: LAWFULL (1 << 4) - Note the legacy spelling
    /// </summary>
    Lawful = 1 << 4,
    
    /// <summary>
    /// Neutral zone (not yet implemented in legacy).
    /// Legacy: NEUTRAL (1 << 5)
    /// </summary>
    Neutral = 1 << 5,
    
    /// <summary>
    /// Chaotic - random exit regardless of direction chosen.
    /// Legacy: CHAOTIC (1 << 6)
    /// </summary>
    Chaotic = 1 << 6,
    
    /// <summary>
    /// No magic can be cast in this room.
    /// Legacy: NO_MAGIC (1 << 7)
    /// </summary>
    NoMagic = 1 << 7,
    
    /// <summary>
    /// Tunnel - only one person can enter at a time (not yet implemented).
    /// Legacy: TUNNEL (1 << 8)
    /// </summary>
    Tunnel = 1 << 8,
    
    /// <summary>
    /// Private - if 2+ people inside, no scrying or entry.
    /// Legacy: PRIVATE (1 << 9)
    /// </summary>
    Private = 1 << 9,
    
    /// <summary>
    /// God room - no mortal entry without being transported.
    /// Legacy: GODROOM (1 << 10)
    /// </summary>
    GodRoom = 1 << 10,
    
    /// <summary>
    /// BFS pathfinding mark (internal tracking flag).
    /// Legacy: BFS_MARK (1 << 11)
    /// </summary>
    BfsMark = 1 << 11,
    
    /// <summary>
    /// Removes all mana from players in the room.
    /// Legacy: ZERO_MANA (1 << 12)
    /// </summary>
    ZeroMana = 1 << 12,
    
    /// <summary>
    /// Dispels all non-innate player affects.
    /// Legacy: DISPELL (1 << 13)
    /// </summary>
    Dispel = 1 << 13,
    
    /// <summary>
    /// Silent room - only says, tells, and wizard communication allowed.
    /// Legacy: SILENT (1 << 14)
    /// </summary>
    Silent = 1 << 14,
    
    /// <summary>
    /// In air - only flying movement allowed.
    /// Legacy: IN_AIR (1 << 15)
    /// </summary>
    InAir = 1 << 15,
    
    /// <summary>
    /// OCS system marker (online creation system).
    /// Legacy: OCS (1 << 16)
    /// </summary>
    Ocs = 1 << 16,
    
    /// <summary>
    /// Player killing allowed in this room.
    /// Legacy: PKOK (1 << 17)
    /// </summary>
    PkOk = 1 << 17,
    
    /// <summary>
    /// Arena/wargames room.
    /// Legacy: ARENA (1 << 18)
    /// </summary>
    Arena = 1 << 18,
    
    /// <summary>
    /// Faster HP regeneration in this room.
    /// Legacy: REGEN (1 << 19)
    /// </summary>
    Regen = 1 << 19
}
