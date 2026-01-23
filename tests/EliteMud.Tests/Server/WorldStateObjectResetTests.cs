using EliteMud.Application.World;
using EliteMud.Game;

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
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, new List<ObjectAffect>());

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
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, new List<ObjectAffect>());

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
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, new List<ObjectAffect>());

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

    [Fact]
    public void ResetZone_EquipsMobWithItem_WhenSpawnChanceSucceeds()
    {
        // Arrange - Recreate Henrietta's dress scenario
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [6714] = new(6714, "Henrietta's Room", "Desc", new List<ExitDefinition>())
        });

        var mobDef = new MobDefinition(
            6710, "Henrietta", "Short", "Long", "Desc", 100, "Elf", "Herbalist",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 100, 1000);

        var dressDef = new ObjectDefinition(
            6709, "dress", "Henrietta's favorite dress", "Long", "Desc", "Armor",
            new List<string>(), new List<string>(), null, new List<int>(), 5, 15000, new List<ObjectAffect>());

        var zoneDef = new ZoneDefinition(
            66, "Tynstri", new RoomRange(6700, 6799), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                // Load Henrietta
                new("LoadMob", ObjectId: null, MobId: 6710, RoomId: 6714, MaxExisting: 1,
                    SpawnChance: null, EquipSlot: null, ContainerId: null, 
                    DoorDirection: null, DoorState: null, IfFlag: false),
                // Equip dress with 100% chance (not 1% for testing)
                new("EquipMob", ObjectId: 6709, MobId: null, RoomId: null, MaxExisting: null,
                    SpawnChance: 100, EquipSlot: 5, ContainerId: null,
                    DoorDirection: null, DoorState: null, IfFlag: true)
            });

        var mobDefs = new Dictionary<int, MobDefinition> { [6710] = mobDef };
        var objDefs = new Dictionary<int, ObjectDefinition> { [6709] = dressDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [6714] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [6714] = new() };
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        worldState.ResetZone(66);

        // Assert
        var mobs = worldState.GetMobsInRoom(6714);
        Assert.Single(mobs);
        
        var henrietta = mobs[0];
        Assert.Equal(6710, henrietta.Definition.Id);
        
        // Check that Henrietta is wearing the dress
        Assert.Single(henrietta.Equipment);
        Assert.True(henrietta.Equipment.ContainsKey(EquipmentSlot.Body));
        
        var dress = henrietta.Equipment[EquipmentSlot.Body];
        Assert.Equal(6709, dress.Definition.Id);
        Assert.Equal("dress", dress.Definition.Name);
    }

    [Fact]
    public void ResetZone_DoesNotEquipMob_WhenSpawnChanceFails()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var mobDef = new MobDefinition(
            100, "Mob", "Short", "Long", "Desc", 1, "Human", "Warrior",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10);

        var swordDef = new ObjectDefinition(
            200, "sword", "A sword", "Long", "Desc", "Weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, new List<ObjectAffect>());

        var zoneDef = new ZoneDefinition(
            1, "Test Zone", new RoomRange(1, 1), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                new("LoadMob", ObjectId: null, MobId: 100, RoomId: 1, MaxExisting: 1,
                    SpawnChance: null, EquipSlot: null, ContainerId: null,
                    DoorDirection: null, DoorState: null, IfFlag: false),
                // 0% spawn chance - should never equip
                new("EquipMob", ObjectId: 200, MobId: null, RoomId: null, MaxExisting: null,
                    SpawnChance: 0, EquipSlot: 16, ContainerId: null,
                    DoorDirection: null, DoorState: null, IfFlag: true)
            });

        var mobDefs = new Dictionary<int, MobDefinition> { [100] = mobDef };
        var objDefs = new Dictionary<int, ObjectDefinition> { [200] = swordDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        worldState.ResetZone(1);

        // Assert
        var mobs = worldState.GetMobsInRoom(1);
        Assert.Single(mobs);
        
        var mob = mobs[0];
        Assert.Empty(mob.Equipment); // No equipment due to 0% spawn chance
    }

    [Fact]
    public void ResetZone_DoesNotEquipMob_WhenIfFlagSetAndMobNotSpawned()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var swordDef = new ObjectDefinition(
            200, "sword", "A sword", "Long", "Desc", "Weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, new List<ObjectAffect>());

        var zoneDef = new ZoneDefinition(
            1, "Test Zone", new RoomRange(1, 1), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                // Try to equip with IfFlag=true, but no mob was loaded before
                new("EquipMob", ObjectId: 200, MobId: null, RoomId: null, MaxExisting: null,
                    SpawnChance: 100, EquipSlot: 16, ContainerId: null,
                    DoorDirection: null, DoorState: null, IfFlag: true)
            });

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition> { [200] = swordDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition> { zoneDef };

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Act
        worldState.ResetZone(1);

        // Assert - no mobs should exist
        var mobs = worldState.GetMobsInRoom(1);
        Assert.Empty(mobs);
    }
}
