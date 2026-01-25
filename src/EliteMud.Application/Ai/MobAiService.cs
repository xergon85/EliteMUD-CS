using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Ai;

/// <summary>
/// Handles mob AI behaviors based on legacy mobile_activity() from mobact.c.
/// Implements hybrid C#/Lua approach:
/// - Core behaviors (aggro, wander, memory, assist) in C#
/// - Lua hooks for custom mob-specific overrides
/// 
/// Processing order per tick (matches legacy mobact.c:101-273):
/// 1. Check if awake and not fighting
/// 2. Scavenger behavior
/// 3. Tracking (follow path)
/// 4. Random wandering
/// 5. Sentinel return home
/// 6. Aggressive behavior
/// 7. Memory/hunting behavior
/// </summary>
public class MobAiService
{
    private readonly IScriptEngine _scriptEngine;
    private readonly PathfindingService _pathfinding;
    private readonly Random _random = new();

    public MobAiService(IScriptEngine scriptEngine, PathfindingService pathfinding)
    {
        _scriptEngine = scriptEngine;
        _pathfinding = pathfinding;
    }

    /// <summary>
    /// Process AI for a single mob.
    /// Called every PULSE_VIOLENCE (2 seconds) or on a separate PULSE_MOBILE tick.
    /// Legacy: mobile_activity() in mobact.c:101-273
    /// </summary>
    public void ProcessMobTick(
        MobInstance mob,
        int roomId,
        IWorldState worldState,
        IReadOnlyDictionary<int, PlayerConnection> connections)
    {
        // Legacy: Skip if mob not in valid room or is switched
        if (roomId < 0)
        {
            return;
        }

        // Lua hook: OnMobTick - allows complete override of mob behavior
        // If script returns true, skip default AI
        var luaOverride = TryInvokeLuaHook(mob, roomId, worldState, connections);
        if (luaOverride)
        {
            return;
        }

        // Only process AI if awake and not fighting
        // Legacy: mobact.c:144
        if (!IsAwake(mob) || mob.FightingConnectionId != null || mob.FightingMobInstanceId != null)
        {
            return;
        }

        // 1. Scavenger behavior (pick up valuable items)
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Scavenger))
        {
            ProcessScavenger(mob, roomId, worldState);
        }

        // 2. Tracking (follow pre-computed path)
        if (mob.TrackingPath != null && mob.TrackingPath.Count > 0)
        {
            ProcessTracking(mob, roomId, worldState);
            return; // Don't wander if tracking
        }

        // 3. Random wandering (or sentinel return home)
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Sentinel))
        {
            ProcessSentinelReturnHome(mob, roomId, worldState);
        }
        else
        {
            ProcessWandering(mob, roomId, worldState);
        }

        // 4. Aggressive behavior (attack players on sight)
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Aggressive))
        {
            ProcessAggressive(mob, roomId, worldState, connections);
        }

        // 5. Memory behavior (hunt down attackers)
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Memory) && mob.Memory.Count > 0)
        {
            ProcessMemory(mob, roomId, worldState, connections);
        }
    }

    /// <summary>
    /// Process assist/helper behavior when a mob enters combat.
    /// Called from hit() function when combat starts.
    /// Legacy: fight.c:1575-1600
    /// </summary>
    public void ProcessAssist(
        MobInstance aggressor,
        int roomId,
        IWorldState worldState,
        IReadOnlyDictionary<int, PlayerConnection> connections)
    {
        // Get all mobs in the room
        var mobsInRoom = worldState.GetMobsInRoom(roomId);

        foreach (var helper in mobsInRoom)
        {
            // Skip self
            if (helper.InstanceId == aggressor.InstanceId)
            {
                continue;
            }

            // Check if this mob is a helper
            if (!helper.Definition.ParsedFlags.HasFlag(MobFlags.Helper))
            {
                continue;
            }

            // Already fighting - can't assist
            if (helper.FightingConnectionId != null || helper.FightingMobInstanceId != null)
            {
                continue;
            }

            // TODO: Check for WAIT_STATE (mob cooldown)
            // if (helper.WaitState > 0) continue;

            // Determine who to assist
            // Legacy logic: if helper has master (charmed), assist master
            // Otherwise, assist mobs with similar alignment (within 750 points)

            // TODO: Check if helper is charmed/following
            // For now, use alignment-based assistance
            int alignmentDifference = Math.Abs(aggressor.Definition.Alignment - helper.Definition.Alignment);
            if (alignmentDifference <= 750)
            {
                // Helper should attack the aggressor's target
                if (aggressor.FightingConnectionId != null)
                {
                    helper.FightingConnectionId = aggressor.FightingConnectionId;
                    helper.Position = Position.Fighting;
                    // TODO: Send message to room about helper joining combat
                }
                else if (aggressor.FightingMobInstanceId != null)
                {
                    helper.FightingMobInstanceId = aggressor.FightingMobInstanceId;
                    helper.Position = Position.Fighting;
                }
            }
        }
    }

    // ===== Private Helper Methods =====

    private bool IsAwake(MobInstance mob)
    {
        // Legacy: AWAKE(ch) macro checks position >= POS_SLEEPING
        return mob.Position >= Position.Sleeping;
    }

    /// <summary>
    /// Try to invoke Lua OnMobTick hook.
    /// Returns true if script handled the tick (skip default AI).
    /// </summary>
    private bool TryInvokeLuaHook(
        MobInstance mob,
        int roomId,
        IWorldState worldState,
        IReadOnlyDictionary<int, PlayerConnection> connections)
    {
        // TODO: Implement Lua hook invocation
        // For now, return false (always use default AI)
        return false;
    }

    private void ProcessScavenger(MobInstance mob, int roomId, IWorldState worldState)
    {
        // Legacy: mobact.c:145-163
        // 1 in 11 chance per tick (~9%)
        if (_random.Next(0, 11) != 0)
        {
            return;
        }

        var objectsInRoom = worldState.GetObjectsInRoom(roomId);
        if (objectsInRoom.Count == 0)
        {
            return;
        }

        // Find most valuable object mob can pick up
        ObjectInstance? bestObject = null;
        int maxCost = 1;

        foreach (var obj in objectsInRoom)
        {
            // TODO: Add MOB_CAN_GET_OBJ check (item flags, weight limits, etc.)
            if (obj.Definition.Cost > maxCost)
            {
                bestObject = obj;
                maxCost = obj.Definition.Cost;
            }
        }

        if (bestObject != null)
        {
            // Transfer object from room to mob inventory
            worldState.TakeObjectForMob(mob, bestObject.InstanceId, roomId);
            
            // TODO: Send act() message: "$n gets $p."
            // Will need ActMessage service injection in constructor
        }
    }

    private void ProcessTracking(MobInstance mob, int roomId, IWorldState worldState)
    {
        // Legacy: mobact.c:166-176
        if (mob.TrackingPath == null || mob.TrackingPath.Count == 0)
        {
            return;
        }

        // Wake up if sleeping
        if (mob.Position < Position.Standing)
        {
            mob.Position = Position.Standing;
        }

        // Get next room ID from path (path contains room IDs, not directions)
        var nextRoomId = mob.TrackingPath.Dequeue();

        // Validate target room exists
        if (!worldState.World.Rooms.ContainsKey(nextRoomId))
        {
            // Path is invalid, clear it
            mob.TrackingPath = null;
            return;
        }

        // TODO: Check if target room has NO_MOB flag
        
        // Move mob to next room
        if (worldState.MoveMob(mob.InstanceId, roomId, nextRoomId))
        {
            // TODO: Send act() message about mob leaving/entering
        }
        else
        {
            // Movement failed, clear path
            mob.TrackingPath = null;
        }

        // If path is complete, clear it
        if (mob.TrackingPath != null && mob.TrackingPath.Count == 0)
        {
            mob.TrackingPath = null;
        }
    }

    private void ProcessWandering(MobInstance mob, int roomId, IWorldState worldState)
    {
        // Legacy: mobact.c:177-194
        
        // Only wander if standing
        if (mob.Position != Position.Standing)
        {
            return;
        }

        // Random number 0-45, only move if < 6 (6 directions)
        // This gives ~13% chance to move each tick
        int roll = _random.Next(0, 46);
        if (roll >= 6) // NUM_OF_DIRS
        {
            return;
        }

        Direction direction = (Direction)roll; // 0-5 maps to North, East, South, West, Up, Down

        // Anti-bounce: don't immediately go back the way we came
        if (mob.LastDirection == roll)
        {
            mob.LastDirection = -1; // Reset but don't move
            return;
        }

        // Get current room
        if (!worldState.World.Rooms.TryGetValue(roomId, out var currentRoom))
        {
            return;
        }

        // Find exit in chosen direction
        var exit = currentRoom.Exits.FirstOrDefault(e => e.Direction == direction);
        if (exit == null)
        {
            return; // No exit in this direction
        }

        var targetRoomId = exit.TargetRoomId;

        // Validate target room exists
        if (!worldState.World.Rooms.TryGetValue(targetRoomId, out var targetRoom))
        {
            return;
        }

        // Check if target room has NO_MOB or DEATH flags
        // Legacy: mobact.c:186-187
        if (targetRoom.Flags.HasFlag(RoomFlags.NoMob) || targetRoom.Flags.HasFlag(RoomFlags.Death))
        {
            return; // Don't wander into dangerous or restricted rooms
        }

        // Check zone restriction if MOB_STAY_ZONE
        // Legacy: mobact.c:189-192
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.StayZone))
        {
            // Only wander if target room is in same zone
            if (currentRoom.ZoneId != null && targetRoom.ZoneId != null && 
                currentRoom.ZoneId != targetRoom.ZoneId)
            {
                return; // Don't leave zone
            }
        }

        // Attempt to move
        if (worldState.MoveMob(mob.InstanceId, roomId, targetRoomId))
        {
            mob.LastDirection = roll;
            // TODO: Send act() message: mob leaves in direction
            // TODO: Send act() message to new room: mob arrives from opposite direction
        }
    }

    private void ProcessSentinelReturnHome(MobInstance mob, int roomId, IWorldState worldState)
    {
        // Legacy: mobact.c:195-199
        if (mob.Hometown == null || mob.Hometown == roomId)
        {
            return; // Already home or no hometown set
        }

        if (mob.FightingConnectionId != null || mob.FightingMobInstanceId != null)
        {
            return; // Don't return home while fighting
        }

        // Use pathfinding to find way home
        // Legacy: perform_track(ch, ch->player.hometown, 100);
        var path = _pathfinding.FindPath(
            worldState,
            startRoomId: roomId,
            targetRoomId: mob.Hometown.Value,
            maxDistance: 100,
            respectNoMob: true,
            stayInZone: false);

        if (path != null)
        {
            mob.TrackingPath = path;
        }
    }

    private void ProcessAggressive(
        MobInstance mob,
        int roomId,
        IWorldState worldState,
        IReadOnlyDictionary<int, PlayerConnection> connections)
    {
        // Legacy: mobact.c:218-242
        
        // Get players in the same room
        var playersInRoom = connections.Values
            .Where(c => c.Player?.RoomId == roomId)
            .Select(c => c.Player!)
            .ToList();

        if (playersInRoom.Count == 0)
        {
            return;
        }

        foreach (var player in playersInRoom)
        {
            // Skip if can't see player
            // TODO: Implement CAN_SEE(ch, tmp_ch) visibility check
            
            // Skip if player has NOHASSLE flag
            // TODO: Check PRF_FLAGGED(tmp_ch, PRF_NOHASSLE)
            
            // Check wimpy flag - don't attack awake targets if wimpy
            if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Wimpy) && player.Position >= Position.Sleeping)
            {
                continue;
            }

            // Check alignment-specific aggro
            bool shouldAttack = false;

            if (mob.Definition.ParsedFlags.HasFlag(MobFlags.AggressiveEvil))
            {
                // Attack evil players (alignment <= -350)
                shouldAttack = player.Alignment <= -350;
            }
            else if (mob.Definition.ParsedFlags.HasFlag(MobFlags.AggressiveGood))
            {
                // Attack good players (alignment >= 350)
                shouldAttack = player.Alignment >= 350;
            }
            else if (mob.Definition.ParsedFlags.HasFlag(MobFlags.AggressiveNeutral))
            {
                // Attack neutral players (alignment -349 to +349)
                shouldAttack = player.Alignment > -350 && player.Alignment < 350;
            }
            else
            {
                // No alignment restriction - attack anyone
                shouldAttack = true;
            }

            if (shouldAttack)
            {
                // Attack this player - initiate combat
                // Legacy: hit(ch, tmp_ch, 0) -> set_fighting(ch, victim) + set_fighting(victim, ch)
                var connectionId = connections.First(kvp => kvp.Value.Player == player).Key;
                
                // Set mob to fight player
                mob.FightingConnectionId = connectionId;
                mob.Position = Position.Fighting;
                
                // Set player to fight mob (victim fights back automatically)
                player.FightingConnectionId = -mob.InstanceId; // Negative for mobs
                if (player.Position > Position.Fighting)
                {
                    player.Position = Position.Fighting;
                }
                
                // TODO: Send act() message about mob attacking
                
                // Found a target, stop searching
                return;
            }
        }
    }

    private void ProcessMemory(
        MobInstance mob,
        int roomId,
        IWorldState worldState,
        IReadOnlyDictionary<int, PlayerConnection> connections)
    {
        // Legacy: mobact.c:244-270
        
        // Find remembered players
        PlayerState? victim = null;
        
        foreach (var connection in connections.Values)
        {
            if (connection.Player == null)
            {
                continue;
            }

            // Check if this player is in mob's memory
            if (!mob.Memory.Contains(connection.Player.Id))
            {
                continue;
            }

            // TODO: Check visibility with CAN_SEE(ch, tmp_ch)

            victim = connection.Player;

            // If victim is in same room, attack or follow
            if (victim.RoomId == roomId)
            {
                // Check if room has LAWFUL flag
                // Legacy: mobact.c:256 - if (!IS_SET(world[ch->in_room]->room_flags, LAWFULL))
                var currentRoom = worldState.World.Rooms[roomId];
                bool isLawfulRoom = currentRoom.Flags.HasFlag(RoomFlags.Lawful);

                if (!isLawfulRoom)
                {
                    // Attack the remembered enemy
                    // Legacy: hit(ch, vict, 0) -> set_fighting(ch, victim) + set_fighting(victim, ch)
                    // TODO: Send act() message: "'Hey! You're the fiend that attacked me!!!', exclaims $n."
                    var connectionId = connections.First(kvp => kvp.Value.Player == connection.Player).Key;
                    
                    // Set mob to fight player
                    mob.FightingConnectionId = connectionId;
                    mob.Position = Position.Fighting;
                    
                    // Set player to fight mob
                    victim.FightingConnectionId = -mob.InstanceId;
                    if (victim.Position > Position.Fighting)
                    {
                        victim.Position = Position.Fighting;
                    }
                    
                    return;
                }
                else
                {
                    // In lawful room, just follow instead of attacking
                    // TODO: Call do_follow(ch, GET_NAME(vict), 0, 0)
                }
            }
        }

        // If victim not in same room, track them
        if (victim != null && victim.RoomId != roomId)
        {
            // Only track if not sentinel and mob is healthy enough
            if (!mob.Definition.ParsedFlags.HasFlag(MobFlags.Sentinel) &&
                mob.HitPoints + mob.Level > mob.MaxHitPoints)
            {
                // TODO: Implement annoy_hunted_victim() - random taunts
                
                // Use pathfinding to hunt down the victim
                // Legacy: perform_track(ch, IN_ROOM(vict), GET_LEVEL(ch))
                var maxDistance = Math.Max((int)mob.Level, 10); // Use mob level as max distance, minimum 10
                
                var path = _pathfinding.FindPath(
                    worldState,
                    startRoomId: roomId,
                    targetRoomId: victim.RoomId,
                    maxDistance: maxDistance,
                    respectNoMob: true,
                    stayInZone: false);

                if (path != null)
                {
                    mob.TrackingPath = path;
                }
            }
        }
    }
}

/// <summary>
/// Represents a player connection for mob AI processing.
/// Temporary struct until we refactor connection management.
/// </summary>
public class PlayerConnection
{
    public required int ConnectionId { get; init; }
    public PlayerState? Player { get; set; }
}
