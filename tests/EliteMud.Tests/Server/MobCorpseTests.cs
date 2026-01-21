using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

public class MobCorpseTests
{
    [Fact]
    public void CreateMobCorpse_TransfersEquipmentToCorpse()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var mobDef = new MobDefinition(
            100, "rat", "a small rat", "A small rat is here.", "Desc", 1, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>());

        var swordDef = new ObjectDefinition(
            200, "sword", "a rusty sword", "A rusty sword is here.", "Desc", "weapon",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100);

        var helmetDef = new ObjectDefinition(
            201, "helmet", "a leather helmet", "A leather helmet is here.", "Desc", "armor",
            new List<string>(), new List<string>(), null, new List<int>(), 5, 50);

        var mobDefs = new Dictionary<int, MobDefinition> { [100] = mobDef };
        var objDefs = new Dictionary<int, ObjectDefinition> { [200] = swordDef, [201] = helmetDef };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Create a mob
        var mob = new MobInstance(1, mobDef);
        
        // Equip the mob with a sword and helmet
        var sword = new ObjectInstance(100, swordDef);
        var helmet = new ObjectInstance(101, helmetDef);
        mob.Equip(sword, EquipmentSlot.Wield);
        mob.Equip(helmet, EquipmentSlot.Head);

        // Act - Create corpse
        var corpse = worldState.CreateMobCorpse(mob, 1);

        // Assert
        Assert.NotNull(corpse);
        Assert.Equal(2, corpse.Contents.Count);
        Assert.Contains(corpse.Contents, obj => obj.InstanceId == 100); // Sword
        Assert.Contains(corpse.Contents, obj => obj.InstanceId == 101); // Helmet
        Assert.Empty(mob.Equipment); // Mob should have no equipment left
    }

    [Fact]
    public void CreateMobCorpse_WithNoEquipment_CreatesEmptyCorpse()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var mobDef = new MobDefinition(
            100, "rat", "a small rat", "A small rat is here.", "Desc", 1, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>());

        var mobDefs = new Dictionary<int, MobDefinition> { [100] = mobDef };
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Create a mob with no equipment
        var mob = new MobInstance(1, mobDef);

        // Act
        var corpse = worldState.CreateMobCorpse(mob, 1);

        // Assert
        Assert.NotNull(corpse);
        Assert.Empty(corpse.Contents);
    }

    [Fact]
    public void CreateMobCorpse_CleansNewlinesFromMobShortDescription()
    {
        // Arrange
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Desc", new List<ExitDefinition>())
        });

        var mobDef = new MobDefinition(
            100, "halfling", "The halfling\n", "The halfling is here.\n", "Desc", 1, "Humanoid", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>());

        var mobDefs = new Dictionary<int, MobDefinition> { [100] = mobDef };
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
        var mob = new MobInstance(1, mobDef);

        // Act
        var corpse = worldState.CreateMobCorpse(mob, 1);

        // Assert
        Assert.NotNull(corpse);
        Assert.DoesNotContain("\n", corpse.Definition.LongDescription);
        Assert.DoesNotContain("\r", corpse.Definition.LongDescription);
        Assert.Equal("The corpse of The halfling is lying here.", corpse.Definition.LongDescription);
    }
}
