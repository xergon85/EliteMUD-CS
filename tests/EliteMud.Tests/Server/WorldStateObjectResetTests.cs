using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

public class WorldStateObjectResetTests
{
    [Fact]
    public void ResetZone_LoadsObjectsIntoRooms()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var objDef = new ObjectDefinition(
            100, "Sword", "Short", "Long", "Desc", "Weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100);

        var zoneDef = new ZoneDefinition(
            1, "Test Zone", new RoomRange(1, 1), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                new("LoadObject", ObjectId: 100, MobId: null, RoomId: 1, MaxExisting: null, 
                    SpawnChance: 100, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null, IfFlag: false)
            });

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition> { [100] = objDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        var success = worldState.ResetZone(1);

        // Assert
        Assert.True(success);
        var objects = worldState.GetObjectsInRoom(1);
        Assert.Single(objects);
        Assert.Equal(100, objects[0].Definition.Id);
    }

    [Fact]
    public void ResetZone_LoadsObject_WithGuaranteedSpawnChance()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var objDef = new ObjectDefinition(
            100, "Sword", "Short", "Long", "Desc", "Weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100);

        var zoneDef = new ZoneDefinition(
            1, "Test Zone", new RoomRange(1, 1), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                new("LoadObject", ObjectId: 100, MobId: null, RoomId: 1, MaxExisting: null, 
                    SpawnChance: 100, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null, IfFlag: false)
            });

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition> { [100] = objDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        
        // Pre-populate one object
        var roomObjs = new Dictionary<int, List<ObjectInstance>> 
        { 
            [1] = new() { new ObjectInstance(99, objDef) } 
        };
        
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        worldState.ResetZone(1);

        // Assert - with 100% spawn chance, one new object should always spawn
        // Note: existing objects are cleared first (since no MaxExisting), then new one spawns
        var objects = worldState.GetObjectsInRoom(1);
        Assert.Single(objects);
        Assert.NotEqual(99, objects[0].InstanceId); // Should be a new object
    }

    [Fact]
    public void ResetZone_ClearsExistingObjects_IfNotLimited()
    {
        // Note: EliteMUD legacy behavior usually clears rooms on reset unless specific flags/modes prevent it.
        // Our current implementation in WorldState.ClearZoneRooms clears everything.
        // This test verifies that behavior.

        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var objDef = new ObjectDefinition(
            100, "Sword", "Short", "Long", "Desc", "Weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100);

        // No load commands, just verify clearing
        var zoneDef = new ZoneDefinition(
            1, "Test Zone", new RoomRange(1, 1), "ResetAlways",
            new List<ZoneResetDefinition>());

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition> { [100] = objDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        
        // Pre-populate "trash"
        var roomObjs = new Dictionary<int, List<ObjectInstance>> 
        { 
            [1] = new() { new ObjectInstance(99, objDef) } 
        };
        
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        worldState.ResetZone(1);

        // Assert
        var objects = worldState.GetObjectsInRoom(1);
        Assert.Empty(objects);
    }
}
