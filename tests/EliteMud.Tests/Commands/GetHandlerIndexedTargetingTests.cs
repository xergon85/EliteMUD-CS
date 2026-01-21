using EliteMud.Application.Commands.Get;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Commands;

public class GetHandlerIndexedTargetingTests
{
    [Fact]
    public void Get_FromFirstCorpse_Success()
    {
        // Arrange
        var corpse1 = CreateCorpse(1, "corpse of rat");
        var corpse2 = CreateCorpse(2, "corpse of bat");
        var sword = CreateItem(100, "sword");
        
        corpse1.AddItem(sword);
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse1, corpse2 });
        var handler = new GetHandler(worldState);

        // Act - "get sword corpse" (should get from first corpse)
        var result = handler.Handle(player, "sword corpse");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Object);
        Assert.Equal("sword", result.Object.Name);
        Assert.Contains(100, player.InventoryObjectIds);
    }

    [Fact]
    public void Get_FromSecondCorpse_WithIndexedTargeting_Success()
    {
        // Arrange
        var corpse1 = CreateCorpse(1, "corpse");
        var corpse2 = CreateCorpse(2, "corpse");
        var shield = CreateItem(100, "shield");
        var sword = CreateItem(101, "sword");
        
        corpse1.AddItem(shield);
        corpse2.AddItem(sword);
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse1, corpse2 });
        var handler = new GetHandler(worldState);

        // Act - "get sword 2.corpse" (should get from second corpse)
        var result = handler.Handle(player, "sword 2.corpse");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Object);
        Assert.Equal("sword", result.Object.Name);
        Assert.Contains(101, player.InventoryObjectIds);
        Assert.Single(corpse1.Contents); // First corpse still has its item
        Assert.Empty(corpse2.Contents);  // Second corpse is now empty
    }

    [Fact]
    public void Get_FromThirdCorpse_WithIndexedTargeting_Success()
    {
        // Arrange
        var corpse1 = CreateCorpse(1, "corpse");
        var corpse2 = CreateCorpse(2, "corpse");
        var corpse3 = CreateCorpse(3, "corpse");
        var helmet = CreateItem(100, "helmet");
        
        corpse3.AddItem(helmet);
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse1, corpse2, corpse3 });
        var handler = new GetHandler(worldState);

        // Act - "get helmet 3.corpse"
        var result = handler.Handle(player, "helmet 3.corpse");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Object);
        Assert.Equal("helmet", result.Object.Name);
        Assert.Contains(100, player.InventoryObjectIds);
    }

    [Fact]
    public void Get_AllFromSecondCorpse_WithIndexedTargeting_Success()
    {
        // Arrange
        var corpse1 = CreateCorpse(1, "corpse");
        var corpse2 = CreateCorpse(2, "corpse");
        var sword = CreateItem(100, "sword");
        var shield = CreateItem(101, "shield");
        
        corpse2.AddItem(sword);
        corpse2.AddItem(shield);
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse1, corpse2 });
        var handler = new GetHandler(worldState);

        // Act - "get all 2.corpse"
        var result = handler.Handle(player, "all 2.corpse");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("2 items", result.Message);
        Assert.Contains(100, player.InventoryObjectIds);
        Assert.Contains(101, player.InventoryObjectIds);
        Assert.Empty(corpse2.Contents);
    }

    [Fact]
    public void Get_WithInvalidIndex_Fails()
    {
        // Arrange
        var corpse = CreateCorpse(1, "corpse");
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse });
        var handler = new GetHandler(worldState);

        // Act - "get sword 5.corpse" (only 1 corpse exists)
        var result = handler.Handle(player, "sword 5.corpse");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("don't have", result.Message);
    }

    [Fact]
    public void Get_WithInvalidFormat_Fails()
    {
        // Arrange
        var corpse = CreateCorpse(1, "corpse");
        
        var (worldState, player) = CreateTestWorld(new List<ObjectInstance> { corpse });
        var handler = new GetHandler(worldState);

        // Act - "get sword abc.corpse" (invalid number format)
        var result = handler.Handle(player, "sword abc.corpse");

        // Assert
        Assert.False(result.Success);
    }

    // TODO: Re-enable when "all.X" pattern is implemented at CommandHandler level
    // [Fact]
    // public void Get_AllFromAllCorpses_Success()
    // {
    //     ...
    // }

    // [Fact]
    // public void Get_SpecificItemFromAllCorpses_TakesOneFromEach()
    // {
    //     ...
    // }

    private static (WorldState worldState, PlayerState player) CreateTestWorld(List<ObjectInstance>? roomObjects = null)
    {
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Test Room", "A test room.", new List<ExitDefinition>())
        });

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = roomObjects ?? new() };
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
        var player = new PlayerState(1, "TestPlayer", roomId: 1);

        return (worldState, player);
    }

    private static ObjectInstance CreateCorpse(int instanceId, string name)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"the {name}",
            LongDescription: $"The {name} is lying here.",
            Description: "A corpse.",
            Type: "container",
            WearSlots: new List<string>(),
            Flags: new List<string>(),
            Details: null,
            Values: new List<int> { 0, 0, 0, 1 }, // value[3]=1 for NPC corpse
            Weight: 50,
            Cost: 0
        );
        return new ObjectInstance(instanceId, def);
    }

    private static ObjectInstance CreateItem(int instanceId, string name)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"It's a {name}.",
            Type: "weapon",
            WearSlots: new List<string>(),
            Flags: new List<string>(),
            Details: null,
            Values: new List<int> { 0, 0, 0, 0 },
            Weight: 10,
            Cost: 100
        );
        return new ObjectInstance(instanceId, def);
    }
}
