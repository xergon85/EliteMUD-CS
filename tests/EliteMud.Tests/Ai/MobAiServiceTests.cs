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
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        var player = CreatePlayer(1, roomId: 100, alignment: 0);
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should attack player
        Assert.NotNull(mob.Fighting);
        Assert.Same(player, mob.Fighting);
        Assert.Equal(Position.Fighting, mob.Position);
        
        // Player should fight back
        Assert.NotNull(player.Fighting);
        Assert.Same(mob, player.Fighting);
        Assert.Equal(Position.Fighting, player.Position);
    }

    [Fact]
    public void Aggressive_DoesNotAttackWhenNoPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        AddMobToWorld(worldState, mob, 100);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should not be fighting
        Assert.Null(mob.Fighting);
        Assert.Equal(Position.Standing, mob.Position);
    }

    [Fact]
    public void Aggressive_DoesNotAttackWhenAlreadyFighting()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        var player1 = CreatePlayer(1, roomId: 100);
        var player2 = CreatePlayer(2, roomId: 100);
        mob.Fighting = player1; // Already fighting player 1
        mob.Position = Position.Fighting;
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player1);
        EnterGame(worldState, player2);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should still be fighting player 1, not switch to player 2
        Assert.Same(player1, mob.Fighting);
    }

    [Fact]
    public void AggressiveEvil_AttacksEvilPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "paladin", flags: new[] { "AGGRESSIVE", "AGGRESSIVEEVIL" });
        var evilPlayer = CreatePlayer(1, roomId: 100, alignment: -500); // Evil
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, evilPlayer);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should attack evil player
        Assert.NotNull(mob.Fighting);
        Assert.Same(evilPlayer, mob.Fighting);
    }

    [Fact]
    public void AggressiveEvil_DoesNotAttackGoodPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "paladin", flags: new[] { "AGGRESSIVE", "AGGRESSIVEEVIL" });
        var goodPlayer = CreatePlayer(1, roomId: 100, alignment: 500); // Good
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, goodPlayer);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should NOT attack good player
        Assert.Null(mob.Fighting);
    }

    [Fact]
    public void AggressiveGood_AttacksGoodPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "demon", flags: new[] { "AGGRESSIVE", "AGGRESSIVEGOOD" });
        var goodPlayer = CreatePlayer(1, roomId: 100, alignment: 500); // Good
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, goodPlayer);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should attack good player
        Assert.NotNull(mob.Fighting);
        Assert.Same(goodPlayer, mob.Fighting);
    }

    [Fact]
    public void AggressiveNeutral_AttacksNeutralPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "extremist", flags: new[] { "AGGRESSIVE", "AGGRESSIVENEUTRAL" });
        var neutralPlayer = CreatePlayer(1, roomId: 100, alignment: 0); // Neutral
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, neutralPlayer);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should attack neutral player
        Assert.NotNull(mob.Fighting);
        Assert.Same(neutralPlayer, mob.Fighting);
    }

    [Fact]
    public void Wimpy_DoesNotAttackAwakePlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "coward", flags: new[] { "AGGRESSIVE", "WIMPY" });
        var awakePlayer = CreatePlayer(1, roomId: 100, position: Position.Standing);
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, awakePlayer);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Wimpy mob should NOT attack awake player
        Assert.Null(mob.Fighting);
    }

    [Fact]
    public void Aggressive_IgnoresPlayersInOtherRooms()
    {
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "AGGRESSIVE" });
        var player = CreatePlayer(1, roomId: 101);
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        service.ProcessMobTick(mob, 100, worldState);

        Assert.Null(mob.Fighting);
    }

    // ===== Memory Behavior Tests =====

    [Fact]
    public void Memory_AttacksRememberedPlayerInSameRoom()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "vengeful wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        mob.RememberPlayer(player.Id); // Mob remembers this player
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should attack remembered player
        Assert.NotNull(mob.Fighting);
        Assert.Same(player, mob.Fighting);
    }

    [Fact]
    public void Memory_DoesNotAttackInLawfulRoom()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment(lawfulRoom: true);
        var mob = CreateMob(1, "vengeful wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        mob.RememberPlayer(player.Id);
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should NOT attack in lawful room
        Assert.Null(mob.Fighting);
    }

    [Fact]
    public void Memory_DoesNotAttackUnrememberedPlayers()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        // Don't add player to memory
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should NOT attack unremembered player
        Assert.Null(mob.Fighting);
    }

    [Fact]
    public void Memory_RememberingSomeoneElse_LeavesThePlayerHereAlone()
    {
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "wolf", flags: new[] { "MEMORY" });
        var player = CreatePlayer(1, roomId: 100);
        mob.RememberPlayer(2); // someone not in the game
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        service.ProcessMobTick(mob, 100, worldState);

        Assert.Null(mob.Fighting);
    }

    // ===== Wandering Behavior Tests =====

    [Fact]
    public void Wandering_MobCanMoveToAdjacentRoom()
    {
        // Arrange - a roll of 0 is "north", the only exit from room 100
        var (service, worldState) = CreateTestEnvironment(roomsWithExits: true, random: new FixedRandom(0));
        var mob = CreateMob(1, "wanderer");
        AddMobToWorld(worldState, mob, 100);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert
        Assert.Contains(worldState.GetMobsInRoom(101), m => m.InstanceId == 1);
        Assert.DoesNotContain(worldState.GetMobsInRoom(100), m => m.InstanceId == 1);
    }

    [Fact]
    public void Wandering_DoesNotPassClosedExit()
    {
        // Arrange - same setup as above, but the north exit is closed
        var (service, worldState) = CreateTestEnvironment(roomsWithExits: true, random: new FixedRandom(0));
        var mob = CreateMob(1, "wanderer");
        AddMobToWorld(worldState, mob, 100);
        CloseExit(worldState, 100, Direction.North);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert
        Assert.Contains(worldState.GetMobsInRoom(100), m => m.InstanceId == 1);
    }

    [Fact]
    public void Wandering_DoesNotMoveWhenNotStanding()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment(roomsWithExits: true);
        var mob = CreateMob(1, "sleeper");
        mob.Position = Position.Sleeping;
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 50; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
        }

        // Assert - Mob should NOT have moved (still in room 100)
        Assert.NotEmpty(worldState.GetMobsInRoom(100).Where(m => m.InstanceId == 1));
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    [Fact]
    public void Wandering_DoesNotEnterNoMobRoom()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment(roomsWithExits: true, noMobTarget: true);
        var mob = CreateMob(1, "wanderer");
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 100; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
        }

        // Assert - Mob should NOT have moved to NO_MOB room
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    [Fact]
    public void StayZone_DoesNotLeaveZone()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment(roomsWithExits: true, differentZones: true);
        var mob = CreateMob(1, "guard", flags: new[] { "STAYZONE" });
        AddMobToWorld(worldState, mob, 100);

        // Act - Try multiple times
        for (int i = 0; i < 100; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
        }

        // Assert - Mob should NOT have left zone (room 101 is different zone)
        Assert.Empty(worldState.GetMobsInRoom(101).Where(m => m.InstanceId == 1));
    }

    // ===== Helper/Assist Behavior Tests =====

    [Fact]
    public void Helper_AssistsAllyInCombat()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "guard1", flags: Array.Empty<string>(), alignment: 0);
        var helper = CreateMob(2, "guard2", flags: new[] { "HELPER" }, alignment: 0);
        var player = CreatePlayer(1, roomId: 100);
        
        aggressor.Fighting = player;
        aggressor.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessAssist(aggressor, 100, worldState);

        // Assert - Helper should assist aggressor and fight player
        Assert.NotNull(helper.Fighting);
        Assert.Same(player, helper.Fighting);
        Assert.Equal(Position.Fighting, helper.Position);
    }

    [Fact]
    public void Helper_DoesNotAssistDifferentAlignment()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "evil guard", flags: Array.Empty<string>(), alignment: -900); // Evil
        var helper = CreateMob(2, "good guard", flags: new[] { "HELPER" }, alignment: 900); // Good
        var player = CreatePlayer(1, roomId: 100);
        
        aggressor.Fighting = player;
        aggressor.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessAssist(aggressor, 100, worldState);

        // Assert - Helper should NOT assist (alignment difference > 750)
        Assert.Null(helper.Fighting);
    }

    [Fact]
    public void Helper_DoesNotAssistWhenAlreadyFighting()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var aggressor = CreateMob(1, "guard1", flags: Array.Empty<string>(), alignment: 0);
        var helper = CreateMob(2, "guard2", flags: new[] { "HELPER" }, alignment: 0);
        var player1 = CreatePlayer(1, roomId: 100);
        var player2 = CreatePlayer(2, roomId: 100);
        
        aggressor.Fighting = player1;
        aggressor.Position = Position.Fighting;
        
        helper.Fighting = player2; // Already fighting player 2
        helper.Position = Position.Fighting;
        
        AddMobToWorld(worldState, aggressor, 100);
        AddMobToWorld(worldState, helper, 100);
        EnterGame(worldState, player1);
        EnterGame(worldState, player2);

        // Act
        service.ProcessAssist(aggressor, 100, worldState);

        // Assert - Helper should still be fighting player 2, not switch
        Assert.Same(player2, helper.Fighting);
    }

    // ===== Position/State Tests =====

    [Fact]
    public void ProcessMobTick_SkipsWhenNotAwake()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "sleepy cat", flags: new[] { "AGGRESSIVE" });
        mob.Position = Position.Dead; // Not awake
        var player = CreatePlayer(1, roomId: 100);
        AddMobToWorld(worldState, mob, 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, 100, worldState);

        // Assert - Mob should NOT process AI (dead/not awake)
        Assert.Null(mob.Fighting);
    }

    [Fact]
    public void ProcessMobTick_SkipsInvalidRoom()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "cat", flags: new[] { "AGGRESSIVE" });
        var player = CreatePlayer(1, roomId: 100);
        EnterGame(worldState, player);

        // Act
        service.ProcessMobTick(mob, -1, worldState);

        // Assert - Should not crash or process (invalid room)
        Assert.Null(mob.Fighting);
    }

    // ===== Scavenger Tests =====

    [Fact]
    public void Scavenger_PicksUpMostValuableItem()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "rat", flags: new[] { "SCAVENGER" });
        AddMobToWorld(worldState, mob, 100);
        
        // Create objects with different values
        var cheapSword = CreateTestObject(1, "sword", cost: 10);
        var valuableRing = CreateTestObject(2, "ring", cost: 100);
        var cheapBread = CreateTestObject(3, "bread", cost: 1);
        AddObjectToRoom(worldState, cheapSword, 100);
        AddObjectToRoom(worldState, valuableRing, 100);
        AddObjectToRoom(worldState, cheapBread, 100);
        
        // Act - Try multiple times (9% chance per tick)
        bool picked = false;
        for (int i = 0; i < 200; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
            if (mob.Inventory.Any())
            {
                picked = true;
                break;
            }
        }
        
        // Assert - Should pick most valuable (ring)
        Assert.True(picked, "Scavenger should have picked up an item");
        Assert.Contains(2, mob.Inventory.Select(item => item.InstanceId)); // Ring instance ID
        Assert.Single(mob.Inventory);
    }

    [Fact]
    public void Scavenger_HasRandomChance()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "rat", flags: new[] { "SCAVENGER" });
        AddMobToWorld(worldState, mob, 100);
        
        var obj = CreateTestObject(1, "coin", cost: 10);
        AddObjectToRoom(worldState, obj, 100);
        
        // Act - Try 200 times, scavenger should pick up item at least once (9% chance per tick)
        bool pickedUp = false;
        for (int i = 0; i < 200; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
            if (mob.Inventory.Any())
            {
                pickedUp = true;
                break;
            }
        }
        
        // Assert - Should have picked up item (statistically very likely in 200 tries)
        Assert.True(pickedUp, "Scavenger should pick up item with 9% chance (200 tries)");
    }

    [Fact]
    public void Scavenger_IgnoresEmptyRoom()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "rat", flags: new[] { "SCAVENGER" });
        AddMobToWorld(worldState, mob, 100);
        
        // Act - Try multiple times with no objects in room
        for (int i = 0; i < 50; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
        }
        
        // Assert - Mob inventory should be empty
        Assert.Empty(mob.Inventory);
    }

    [Fact]
    public void Scavenger_StoresInInventory()
    {
        // Arrange
        var (service, worldState) = CreateTestEnvironment();
        var mob = CreateMob(1, "rat", flags: new[] { "SCAVENGER" });
        AddMobToWorld(worldState, mob, 100);
        
        var obj = CreateTestObject(1, "gold", cost: 50);
        AddObjectToRoom(worldState, obj, 100);
        
        // Act - Keep trying until object is picked up (9% chance)
        for (int i = 0; i < 200; i++)
        {
            service.ProcessMobTick(mob, 100, worldState);
            if (mob.Inventory.Any())
            {
                break;
            }
        }
        
        // Assert - Object should be in mob inventory
        Assert.Contains(1, mob.Inventory.Select(item => item.InstanceId));
        
        // Object should NOT be in room anymore
        var roomObjects = worldState.GetObjectsInRoom(100);
        Assert.DoesNotContain(roomObjects, o => o.InstanceId == 1);
    }

    // ===== Helper Methods =====

    private (MobAiService service, WorldState worldState)
        CreateTestEnvironment(
            bool roomsWithExits = false, 
            bool lawfulRoom = false,
            bool noMobTarget = false,
            bool differentZones = false,
            Random? random = null)
    {
        var scriptEngine = new MockScriptEngine();
        var pathfinding = new PathfindingService();
        var service = new MobAiService(scriptEngine, pathfinding, random);

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

        return (service, worldState);
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

    private static void CloseExit(WorldState worldState, int roomId, Direction direction)
    {
        // Like AddMobToWorld, set up state through the internal dictionary
        var doorStatesField = typeof(WorldState).GetField("_doorStates",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var doorStates = (Dictionary<(int, Direction), DoorState>)doorStatesField.GetValue(worldState)!;
        doorStates[(roomId, direction)] = new DoorState { IsClosed = true };
    }

    private static void AddMobToWorld(WorldState worldState, MobInstance mob, int roomId) =>
        worldState.Population.Enter(mob, roomId);

    private static void EnterGame(WorldState worldState, PlayerState player) =>
        worldState.Population.Enter(player, player.RoomId);

    private ObjectInstance CreateTestObject(int instanceId, string name, int cost)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"A simple {name}.",
            Type: "treasure",
            WearSlots: new List<string> { "take" },
            Flags: new List<string>(),
            Details: null,
            Values: new List<int> { 0, 0, 0, 0 },
            Weight: 1,
            Cost: cost,
            Affects: new List<ObjectAffect>()
        );
        
        return new ObjectInstance(instanceId, def);
    }

    private void AddObjectToRoom(WorldState worldState, ObjectInstance obj, int roomId)
    {
        // Use reflection to access _roomObjects
        var roomObjectsField = typeof(WorldState).GetField("_roomObjects", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var roomObjects = (Dictionary<int, List<ObjectInstance>>)roomObjectsField.GetValue(worldState)!;
        
        if (!roomObjects.ContainsKey(roomId))
        {
            roomObjects[roomId] = new List<ObjectInstance>();
        }
        
        roomObjects[roomId].Add(obj);
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

/// <summary>
/// Random source that always returns the same roll (clamped to the requested range).
/// </summary>
internal sealed class FixedRandom : Random
{
    private readonly int _value;

    public FixedRandom(int value)
    {
        _value = value;
    }

    public override int Next(int minValue, int maxValue) => Math.Clamp(_value, minValue, maxValue - 1);

    public override int Next(int maxValue) => Math.Clamp(_value, 0, maxValue - 1);

    public override int Next() => _value;

    public override double NextDouble() => 0;
}
