using EliteMud.Application.Commands.Put;
using EliteMud.Application.Commands.Get;
using EliteMud.Application.World;
using EliteMud.Game;
using Xunit;

namespace EliteMud.Tests.Commands;

public class ContainerSystemTests
{
    [Fact]
    public void PutHandler_NoArgument_ReturnsError()
    {
        // Arrange
        var (worldState, player) = CreateTestWorld();
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Put what in what?", result.Message);
    }

    [Fact]
    public void PutHandler_OneArgument_ReturnsError()
    {
        // Arrange
        var (worldState, player) = CreateTestWorld();
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Put what in what?", result.Message);
    }

    [Fact]
    public void PutHandler_ItemNotInInventory_ReturnsError()
    {
        // Arrange
        var bag = CreateContainer(1, "bag");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { bag });
        worldState.TakeObject(player, bag.InstanceId); // Move bag to player inventory
        var handler = new PutHandler(worldState);

        // Act (try to put a sword that doesn't exist)
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("You don't have", result.Message);
    }

    [Fact]
    public void PutHandler_ContainerNotFound_ReturnsError()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword });
        worldState.TakeObject(player, sword.InstanceId);
        var handler = new PutHandler(worldState);

        // Act (try to put sword in a bag that doesn't exist)
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("You don't see", result.Message);
    }

    [Fact]
    public void PutHandler_TargetIsNotContainer_ReturnsError()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var shield = CreateItem(2, "shield"); // Not a container
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, shield });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, shield.InstanceId);
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword shield");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("is not a container", result.Message);
    }

    [Fact]
    public void PutHandler_ContainerIsClosed_ReturnsError()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var bag = CreateContainer(2, "bag", closeable: true, closed: true);
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, bag });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, bag.InstanceId);
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("is closed", result.Message);
    }

    [Fact]
    public void PutHandler_ContainerAtCapacity_ReturnsError()
    {
        // Arrange
        var sword = CreateItem(1, "sword", weight: 100); // Heavy item
        var bag = CreateContainer(2, "bag", capacity: 50); // Small capacity
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, bag });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, bag.InstanceId);
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("is full", result.Message);
    }

    [Fact]
    public void PutHandler_CannotPutItemIntoItself_ReturnsError()
    {
        // Arrange
        var bag = CreateContainer(1, "bag");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { bag });
        worldState.TakeObject(player, bag.InstanceId);
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "bag bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("can't put something inside of itself", result.Message);
    }

    [Fact]
    public void PutHandler_Success_ItemMovedToContainer()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var bag = CreateContainer(2, "bag");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, bag });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, bag.InstanceId);
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("You put", result.Message);
        Assert.DoesNotContain(sword.InstanceId, player.InventoryObjectIds);
        Assert.Contains(sword, bag.Contents);
    }

    [Fact]
    public void PutHandler_ContainerInRoom_CanPutItemIntoIt()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var chest = CreateContainer(2, "chest");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, chest });
        worldState.TakeObject(player, sword.InstanceId);
        // chest remains in room
        var handler = new PutHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword chest");

        // Assert
        Assert.True(result.Success);
        Assert.DoesNotContain(sword.InstanceId, player.InventoryObjectIds);
        Assert.Contains(sword, chest.Contents);
    }

    [Fact]
    public void GetHandler_ContainerIsClosed_CannotGetItem()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var bag = CreateContainer(2, "bag", closeable: true, closed: true);
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, bag });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, bag.InstanceId);
        
        // Put sword in bag manually
        player.RemoveFromInventory(sword.InstanceId);
        bag.AddItem(sword);
        
        var handler = new GetHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("is closed", result.Message);
    }

    [Fact]
    public void GetHandler_ContainerIsOpen_CanGetItem()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var bag = CreateContainer(2, "bag", closeable: true, closed: false);
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, bag });
        worldState.TakeObject(player, sword.InstanceId);
        worldState.TakeObject(player, bag.InstanceId);
        
        // Put sword in bag manually
        player.RemoveFromInventory(sword.InstanceId);
        bag.AddItem(sword);
        
        var handler = new GetHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword bag");

        // Assert
        Assert.True(result.Success);
        Assert.Contains(sword.InstanceId, player.InventoryObjectIds);
        Assert.DoesNotContain(sword, bag.Contents);
    }

    [Fact]
    public void GetHandler_Corpse_CanAlwaysGetItemEvenIfClosed()
    {
        // Arrange
        var sword = CreateItem(1, "sword");
        var corpse = CreateCorpse(2, "corpse");
        var (worldState, player) = CreateTestWorld(roomObjects: new List<ObjectInstance> { sword, corpse });
        worldState.TakeObject(player, sword.InstanceId);
        
        // Put sword in corpse manually
        player.RemoveFromInventory(sword.InstanceId);
        corpse.AddItem(sword);
        corpse.IsClosed = true; // Shouldn't matter for corpses
        
        // Return corpse to room for "get sword corpse" to work
        worldState.DropObject(player, corpse.InstanceId); // Won't work since corpse not in inventory
        // Actually, corpse is still in room, never moved it
        
        var handler = new GetHandler(worldState);

        // Act
        var result = handler.Handle(player, "sword corpse");

        // Assert
        Assert.True(result.Success);
        Assert.Contains(sword.InstanceId, player.InventoryObjectIds);
    }

    [Fact]
    public void ObjectInstance_InitializesClosed_WhenDefinitionHasClosedFlag()
    {
        // Arrange & Act
        var bag = CreateContainer(1, "bag", closeable: true, closed: true);

        // Assert
        Assert.True(bag.IsClosed);
    }

    [Fact]
    public void ObjectInstance_InitializesOpen_WhenDefinitionDoesNotHaveClosedFlag()
    {
        // Arrange & Act
        var bag = CreateContainer(1, "bag", closeable: true, closed: false);

        // Assert
        Assert.False(bag.IsClosed);
    }

    [Fact]
    public void ObjectInstance_CanToggleClosed_RuntimeState()
    {
        // Arrange
        var bag = CreateContainer(1, "bag", closeable: true, closed: false);

        // Act - close the bag
        bag.IsClosed = true;

        // Assert
        Assert.True(bag.IsClosed);

        // Act - reopen the bag
        bag.IsClosed = false;

        // Assert
        Assert.False(bag.IsClosed);
    }

    // Helper methods to create test objects

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

    private static ObjectInstance CreateContainer(
        int instanceId,
        string name,
        int capacity = 100,
        bool closeable = false,
        bool closed = false,
        bool locked = false)
    {
        var flags = new List<string>();
        if (closeable) flags.Add("Closeable");
        if (closed) flags.Add("Closed");
        if (locked) flags.Add("Locked");

        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"You see a {name}.",
            Type: "container",
            WearSlots: Array.Empty<string>(),
            Flags: Array.Empty<string>(),
            Details: new ObjectDetails
            {
                Container = new ObjectContainer(
                    Capacity: capacity,
                    Flags: flags,
                    KeyId: 0,
                    CorpseType: 0,
                    CorpseBlood: 0,
                    CorpseLevel: 0)
            },
            Values: new[] { capacity, closed ? 2 : 0, 0, 0 },
            Weight: 5,
            Cost: 10,
            Affects: Array.Empty<ObjectAffect>()
        );

        return new ObjectInstance(instanceId, def);
    }

    private static ObjectInstance CreateCorpse(int instanceId, string name)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"the corpse of {name}",
            LongDescription: $"The corpse of {name} is lying here.",
            Description: $"You see the corpse of {name}.",
            Type: "container",
            WearSlots: Array.Empty<string>(),
            Flags: Array.Empty<string>(),
            Details: new ObjectDetails
            {
                Container = new ObjectContainer(
                    Capacity: 200,
                    Flags: Array.Empty<string>(),
                    KeyId: 0,
                    CorpseType: 1, // 1 = mob corpse, 2 = player corpse
                    CorpseBlood: 0,
                    CorpseLevel: 1)
            },
            Values: new[] { 200, 0, 0, 1 },
            Weight: 100,
            Cost: 0,
            Affects: Array.Empty<ObjectAffect>()
        );

        return new ObjectInstance(instanceId, def);
    }

    private static ObjectInstance CreateItem(int instanceId, string name, int weight = 10)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"You see a {name}.",
            Type: "weapon",
            WearSlots: new[] { "wield" },
            Flags: Array.Empty<string>(),
            Details: null,
            Values: new[] { 1, 6, 3, 0 },
            Weight: weight,
            Cost: 100,
            Affects: Array.Empty<ObjectAffect>()
        );

        return new ObjectInstance(instanceId, def);
    }
}
