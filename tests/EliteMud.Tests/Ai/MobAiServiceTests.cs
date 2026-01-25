using EliteMud.Application.Ai;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Tests.Ai;

/// <summary>
/// Tests for MobAiService behaviors.
/// Tests aggressive behavior, wandering, memory system, scavenger, sentinel, and assist.
/// Legacy reference: mobile_activity() in mobact.c:101-273
/// </summary>
public class MobAiServiceTests
{
    // ===== Aggressive Behavior Tests =====

    [Fact]
    public void Aggressive_AttacksPlayerInRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        var player = CreatePlayer(1, roomId: 100, alignment: 0);
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should attack player
        Assert.NotNull(mob.FightingConnectionId);
        Assert.Equal(1, mob.FightingConnectionId);
        Assert.Equal(Position.Fighting, mob.Position);
        
        // Player should fight back
        Assert.NotNull(player.FightingConnectionId);
        Assert.Equal(-1, player.FightingConnectionId); // Negative for mob instance ID
        Assert.Equal(Position.Fighting, player.Position);
    }

    [Fact]
    public void Aggressive_DoesNotAttackWhenNoPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        AddMobToWorld(worldState, mob, 100);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should not be fighting
        Assert.Null(mob.FightingConnectionId);
        Assert.Equal(Position.Standing, mob.Position);
    }

    [Fact]
    public void Aggressive_DoesNotAttackWhenAlreadyFighting()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        var player1 = CreatePlayer(1, roomId: 100);
        var player2 = CreatePlayer(2, roomId: 100);
        mob.FightingConnectionId = 1; // Already fighting player 1
        mob.Position = Position.Fighting;
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player1, 1);
        AddPlayerConnection(connections, player2, 2);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should still be fighting player 1, not switch to player 2
        Assert.Equal(1, mob.FightingConnectionId);
    }

    [Fact]
    public void AggressiveEvil_AttacksEvilPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "paladin", flags: new[] { "AGGRESSIVE", "AGGRESSIVEEVIL" });
        var evilPlayer = CreatePlayer(1, roomId: 100, alignment: -500); // Evil
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, evilPlayer, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should attack evil player
        Assert.NotNull(mob.FightingConnectionId);
        Assert.Equal(1, mob.FightingConnectionId);
    }

    [Fact]
    public void AggressiveEvil_DoesNotAttackGoodPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "paladin", flags: new[] { "AGGRESSIVE", "AGGRESSIVEEVIL" });
        var goodPlayer = CreatePlayer(1, roomId: 100, alignment: 500); // Good
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, goodPlayer, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should NOT attack good player
        Assert.Null(mob.FightingConnectionId);
    }

    [Fact]
    public void AggressiveGood_AttacksGoodPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "demon", flags: new[] { "AGGRESSIVE", "AGGRESSIVEGOOD" });
        var goodPlayer = CreatePlayer(1, roomId: 100, alignment: 500); // Good
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, goodPlayer, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should attack good player
        Assert.NotNull(mob.FightingConnectionId);
        Assert.Equal(1, mob.FightingConnectionId);
    }

    [Fact]
    public void AggressiveNeutral_AttacksNeutralPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "extremist", flags: new[] { "AGGRESSIVE", "AGGRESSIVENEUTRAL" });
        var neutralPlayer = CreatePlayer(1, roomId: 100, alignment: 0); // Neutral
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, neutralPlayer, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should attack neutral player
        Assert.NotNull(mob.FightingConnectionId);
        Assert.Equal(1, mob.FightingConnectionId);
    }

    [Fact]
    public void Wimpy_DoesNotAttackAwakePlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "coward", flags: new[] { "AGGRESSIVE", "WIMPY" });
        var awakePlayer = CreatePlayer(1, roomId: 100, position: Position.Standing);
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, awakePlayer, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Wimpy mob should NOT attack awake player
        Assert.Null(mob.FightingConnectionId);
    }

    // ===== Memory Behavior Tests =====

    [Fact]
    public void Memory_AttacksRememberedPlayerInSameRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "vengeful wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        mob.RememberPlayer(player.Id); // Mob remembers this player
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should attack remembered player
        Assert.NotNull(mob.FightingConnectionId);
        Assert.Equal(1, mob.FightingConnectionId);
    }

    [Fact]
    public void Memory_DoesNotAttackInLawfulRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment(lawfulRoom: true);
        var mob = CreateMob(1, "vengeful wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        mob.RememberPlayer(player.Id);
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should NOT attack in lawful room
        Assert.Null(mob.FightingConnectionId);
    }

    [Fact]
    public void Memory_DoesNotAttackUnrememberedPlayers()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        // Don't add player to memory
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should NOT attack unremembered player
        Assert.Null(mob.FightingConnectionId);
    }

    // ===== Wandering Behavior Tests =====

    [Fact]
    public void Wandering_MobCanMoveToAdjacentRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment(roomsWithExits: true);
        var mob = CreateMob(1, "wanderer");
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times since wandering is random (~13% per tick)
        bool moved = false;
        for (int i = 0; i < 100; i++)
        {
            // Find which room the mob is currently in
            int currentRoomId = 100;
            if (worldState.GetMobsInRoom(101).Any(m => m.InstanceId == 1))
            {
                currentRoomId = 101;
            }
            
            service.ProcessMobTick(mob, currentRoomId, worldState, connections);
            
            // Check if mob is now in room 101
            if (worldState.GetMobsInRoom(101).Any(m => m.InstanceId == 1))
            {
                moved = true;
                break;
            }
        }

        // Assert - Mob should eventually move (with high probability)
        Assert.True(moved, "Mob should have wandered to adjacent room after 100 ticks");
    }

    [Fact]
    public void Wandering_DoesNotMoveWhenNotStanding()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment(roomsWithExits: true);
        var mob = CreateMob(1, "sleeper");
        mob.Position = Position.Sleeping;
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 50; i++)
        {
            service.ProcessMobTick(mob, 100, worldState, connections);
        }

        // Assert - Mob should NOT have moved (still in room 100)
        Assert.NotEmpty(worldState.GetMobsInRoom(100).Where(m => m.InstanceId == 1));
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    [Fact]
    public void Wandering_DoesNotEnterNoMobRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment(roomsWithExits: true, noMobTarget: true);
        var mob = CreateMob(1, "wanderer");
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 100; i++)
        {
            service.ProcessMobTick(mob, 100, worldState, connections);
        }

        // Assert - Mob should NOT have moved to NO_MOB room
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    [Fact]
    public void StayZone_DoesNotLeaveZone()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment(roomsWithExits: true, differentZones: true);
        var mob = CreateMob(1, "guard", flags: new[] { "STAYZONE" });
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 100; i++)
        {
            service.ProcessMobTick(mob, 100, worldState, connections);
        }

        // Assert - Mob should NOT have left zone (room 101 is different zone)
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    // ===== Helper/Assist Behavior Tests =====

    [Fact]
    public void Helper_AssistsAllyInCombat()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "guard1", flags: Array.Empty<string>(), alignment: 0);
        var helper = CreateMob(2, "guard2", flags: new[] { "HELPER" }, alignment: 0);
        var player = CreatePlayer(1, roomId: 100);
        
        aggressor.FightingConnectionId = 1;
        aggressor.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessAssist(aggressor, 100, worldState, connections);

        // Assert - Helper should assist aggressor and fight player
        Assert.NotNull(helper.FightingConnectionId);
        Assert.Equal(1, helper.FightingConnectionId);
        Assert.Equal(Position.Fighting, helper.Position);
    }

    [Fact]
    public void Helper_DoesNotAssistDifferentAlignment()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "evil guard", flags: Array.Empty<string>(), alignment: -900); // Evil
        var helper = CreateMob(2, "good guard", flags: new[] { "HELPER" }, alignment: 900); // Good
        var player = CreatePlayer(1, roomId: 100);
        
        aggressor.FightingConnectionId = 1;
        aggressor.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessAssist(aggressor, 100, worldState, connections);

        // Assert - Helper should NOT assist (alignment difference > 750)
        Assert.Null(helper.FightingConnectionId);
    }

    [Fact]
    public void Helper_DoesNotAssistWhenAlreadyFighting()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "guard1", flags: Array.Empty<string>(), alignment: 0);
        var helper = CreateMob(2, "guard2", flags: new[] { "HELPER" }, alignment: 0);
        var player1 = CreatePlayer(1, roomId: 100);
        var player2 = CreatePlayer(2, roomId: 100);
        
        aggressor.FightingConnectionId = 1;
        aggressor.Position = Position.Fighting;
        
        helper.FightingConnectionId = 2; // Already fighting player 2
        helper.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        AddPlayerConnection(connections, player1, 1);
        AddPlayerConnection(connections, player2, 2);

        // Act
        service.ProcessAssist(aggressor, 100, worldState, connections);

        // Assert - Helper should still be fighting player 2, not switch
        Assert.Equal(2, helper.FightingConnectionId);
    }

    // ===== Position/State Tests =====

    [Fact]
    public void ProcessMobTick_SkipsWhenNotAwake()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "sleepy cat", flags: new[] { "AGGRESSIVE" });
        mob.Position = Position.Dead; // Not awake
        var player = CreatePlayer(1, roomId: 100);
        AddMobToWorld(worldState, mob, 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, 100, worldState, connections);

        // Assert - Mob should NOT process AI (dead/not awake)
        Assert.Null(mob.FightingConnectionId);
    }

    [Fact]
    public void ProcessMobTick_SkipsInvalidRoom()
    {
        // Arrange
        var (service, worldState, connections) = CreateTestEnvironment();
        var mob = CreateMob(1, "cat", flags: new[] { "AGGRESSIVE" });
        var player = CreatePlayer(1, roomId: 100);
        AddPlayerConnection(connections, player, 1);

        // Act
        service.ProcessMobTick(mob, -1, worldState, connections);

        // Assert - Should not crash or process (invalid room)
        Assert.Null(mob.FightingConnectionId);
    }

    // ===== Helper Methods =====

    private (MobAiService service, WorldState worldState, Dictionary<int, EliteMud.Application.Ai.PlayerConnection> connections) 
        CreateTestEnvironment(
            bool roomsWithExits = false, 
            bool lawfulRoom = false,
            bool noMobTarget = false,
            bool differentZones = false)
    {
        var scriptEngine = new MockScriptEngine();
        var service = new MobAiService(scriptEngine);

        var roomFlags = lawfulRoom ? RoomFlags.Lawful : RoomFlags.None;
        var targetRoomFlags = noMobTarget ? RoomFlags.NoMob : RoomFlags.None;
        
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [100] = new(100, "Test Room", "A test room.", 
                roomsWithExits ? new List<ExitDefinition> { new(Direction.North, 101) } : new List<ExitDefinition>(),
                roomFlags,
                ZoneId: 1),
            [101] = new(101, "Adjacent Room", "Another room.", 
                new List<ExitDefinition>(),
                targetRoomFlags,
                ZoneId: differentZones ? 2 : 1)
        };
        var worldDef = new WorldDefinition(rooms);

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>> 
        { 
            [100] = new(),
            [101] = new()
        };
        var roomObjs = new Dictionary<int, List<ObjectInstance>>();
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
        var connections = new Dictionary<int, EliteMud.Application.Ai.PlayerConnection>();

        return (service, worldState, connections);
    }

    private MobInstance CreateMob(
        int instanceId,
        string name,
        string[]? flags = null,
        int alignment = 0)
    {
        var def = new MobDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"A {name}.",
            Level: 5,
            Race: "Human",
            Class: "Warrior",
            Flags: (flags ?? Array.Empty<string>()).ToList(),
            Stats: new StatBlock(10, 10, 10, 10, 10, 10),
            Resistances: new List<string>(),
            Skills: new List<string>(),
            ArmorClass: 50,
            MaxHitPoints: 50,
            Alignment: alignment,
            Attacks: new List<MobAttack>(),
            Combat: null
        );

        return new MobInstance(instanceId, def)
        {
            HitPoints = 50
        };
    }

    private PlayerState CreatePlayer(
        int id,
        int roomId,
        int alignment = 0,
        Position position = Position.Standing)
    {
        return new PlayerState(id, $"Player{id}", roomId)
        {
            Alignment = alignment,
            Position = position,
            HitPoints = 100,
            MaxHitPoints = 100
        };
    }

    private void AddMobToWorld(WorldState worldState, MobInstance mob, int roomId)
    {
        // Access the internal mutable list via reflection or use the public API
        // Since WorldState doesn't expose a way to add mobs, we need to access the internal dictionary
        var roomMobsField = typeof(WorldState).GetField("_roomMobs", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (roomMobsField != null)
        {
            var roomMobs = (Dictionary<int, List<MobInstance>>)roomMobsField.GetValue(worldState)!;
            if (!roomMobs.ContainsKey(roomId))
            {
                roomMobs[roomId] = new List<MobInstance>();
            }
            roomMobs[roomId].Add(mob);
        }
    }

    private void AddPlayerConnection(
        Dictionary<int, EliteMud.Application.Ai.PlayerConnection> connections, 
        PlayerState player, 
        int connectionId)
    {
        connections[connectionId] = new EliteMud.Application.Ai.PlayerConnection 
        { 
            ConnectionId = connectionId,
            Player = player 
        };
    }
}

/// <summary>
/// Mock script engine for tests (no-op).
/// </summary>
internal class MockScriptEngine : IScriptEngine
{
    public ValueTask ExecuteAsync(ScriptHook hook, ScriptContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
