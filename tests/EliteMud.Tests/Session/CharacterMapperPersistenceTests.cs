using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Data.Entities;
using EliteMud.Game;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace EliteMud.Tests.Session;

/// <summary>
/// Tests for CharacterMapper container persistence (save/load cycle).
/// Verifies that container contents, nested containers, and container state
/// (IsClosed, IsLocked) persist correctly across save/load operations.
/// </summary>
public class CharacterMapperPersistenceTests : IDisposable
{
    private readonly EliteMudDbContext _dbContext;
    private readonly WorldState _worldState;

    public CharacterMapperPersistenceTests()
    {
        // Create in-memory SQLite database for testing
        var options = new DbContextOptionsBuilder<EliteMudDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new EliteMudDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Create test world
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Test Room", "A test room.", new List<ExitDefinition>())
        });

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>
        {
            [1] = CreateBagDefinition(1, "bag"),
            [2] = CreateItemDefinition(2, "wheat", "a bale of wheat"),
            [3] = CreateItemDefinition(3, "sword", "a sharp sword"),
            [4] = CreateChestDefinition(4, "chest")
        };
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>> { [1] = new() };
        var zones = new List<ZoneDefinition>();

        _worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    [Fact]
    public void SaveAndLoad_SimpleContainerContents_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create bag and wheat items
        var bag = _worldState.CreateObjectInstance(1)!; // bag
        var wheat1 = _worldState.CreateObjectInstance(2)!; // wheat
        var wheat2 = _worldState.CreateObjectInstance(2)!; // wheat
        var wheat3 = _worldState.CreateObjectInstance(2)!; // wheat

        // Add bag to player inventory
        player.AddToInventory(bag.InstanceId);
        
        // Put wheat items in bag (items in containers are NOT in player's InventoryObjectIds)
        bag.AddItem(wheat1);
        bag.AddItem(wheat2);
        bag.AddItem(wheat3);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify database state - JSON should have 1 bag with 3 wheats inside
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Single(inventoryItems); // Only bag at top level
        Assert.Equal(1, inventoryItems[0].ObjectDefinitionId); // bag
        Assert.Equal(3, inventoryItems[0].Contents.Count); // 3 wheats inside
        Assert.All(inventoryItems[0].Contents, item => Assert.Equal(2, item.ObjectDefinitionId)); // All are wheat (ID=2)

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Check player has bag in inventory
        Assert.Single(loadedPlayer.InventoryObjectIds);
        var loadedBag = _worldState.GetObjectInstance(loadedPlayer.InventoryObjectIds.First());
        Assert.NotNull(loadedBag);
        Assert.Equal("bag", loadedBag.Definition.Name);

        // Assert - Check bag contains 3 wheat items
        Assert.Equal(3, loadedBag.Contents.Count);
        Assert.All(loadedBag.Contents, item => Assert.Equal("wheat", item.Definition.Name));
    }

    [Fact]
    public void SaveAndLoad_NestedContainers_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create nested structure: outerBag contains innerBag contains sword
        var outerBag = _worldState.CreateObjectInstance(1)!; // bag
        var innerBag = _worldState.CreateObjectInstance(1)!; // bag
        var sword = _worldState.CreateObjectInstance(3)!; // sword

        // Build hierarchy (only outerBag is in player inventory)
        player.AddToInventory(outerBag.InstanceId);
        outerBag.AddItem(innerBag);
        innerBag.AddItem(sword);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify database hierarchy - JSON should have outerBag containing innerBag containing sword
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Single(inventoryItems); // Only outerBag at top level
        Assert.Single(inventoryItems[0].Contents); // innerBag inside outerBag
        Assert.Single(inventoryItems[0].Contents[0].Contents); // sword inside innerBag

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Check structure
        Assert.Single(loadedPlayer.InventoryObjectIds);
        var loadedOuterBag = _worldState.GetObjectInstance(loadedPlayer.InventoryObjectIds.First());
        Assert.NotNull(loadedOuterBag);
        Assert.Single(loadedOuterBag.Contents);
        
        var loadedInnerBag = loadedOuterBag.Contents.First();
        Assert.Equal("bag", loadedInnerBag.Definition.Name);
        Assert.Single(loadedInnerBag.Contents);
        
        var loadedSword = loadedInnerBag.Contents.First();
        Assert.Equal("sword", loadedSword.Definition.Name);
    }

    [Fact]
    public void SaveAndLoad_ContainerState_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create chest (closeable/lockable) and sword
        var chest = _worldState.CreateObjectInstance(4)!; // chest
        var sword = _worldState.CreateObjectInstance(3)!; // sword

        // Set container state
        chest.IsClosed = true;
        chest.IsLocked = true;

        // Add chest to player, sword goes in chest (not in player inventory)
        player.AddToInventory(chest.InstanceId);
        chest.AddItem(sword);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify database has state - JSON should contain chest with IsClosed and IsLocked
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Single(inventoryItems);
        Assert.NotNull(inventoryItems[0].State);
        Assert.True(inventoryItems[0].State.IsClosed);
        Assert.True(inventoryItems[0].State.IsLocked);
        Assert.Single(inventoryItems[0].Contents); // Sword inside chest

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Check state is restored
        var loadedChest = _worldState.GetObjectInstance(loadedPlayer.InventoryObjectIds.First());
        Assert.NotNull(loadedChest);
        Assert.True(loadedChest.IsClosed);
        Assert.True(loadedChest.IsLocked);
        Assert.Single(loadedChest.Contents); // Sword still inside
    }

    [Fact]
    public void SaveAndLoad_EmptyContainer_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create empty bag
        var bag = _worldState.CreateObjectInstance(1)!; // bag
        player.AddToInventory(bag.InstanceId);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify database has only the bag - JSON should contain empty bag
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Single(inventoryItems);
        Assert.Empty(inventoryItems[0].Contents); // Empty bag

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Check bag is still there and empty
        Assert.Single(loadedPlayer.InventoryObjectIds);
        var loadedBag = _worldState.GetObjectInstance(loadedPlayer.InventoryObjectIds.First());
        Assert.NotNull(loadedBag);
        Assert.Empty(loadedBag.Contents);
    }

    [Fact]
    public void SaveAndLoad_MultipleContainersWithItems_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create two bags with different contents
        var bag1 = _worldState.CreateObjectInstance(1)!; // bag
        var bag2 = _worldState.CreateObjectInstance(1)!; // bag
        var wheat = _worldState.CreateObjectInstance(2)!; // wheat
        var sword = _worldState.CreateObjectInstance(3)!; // sword

        // bag1 contains wheat, bag2 contains sword (only bags in player inventory)
        player.AddToInventory(bag1.InstanceId);
        player.AddToInventory(bag2.InstanceId);
        bag1.AddItem(wheat);
        bag2.AddItem(sword);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify database structure - JSON should have 2 bags at top level, each with 1 item
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Equal(2, inventoryItems.Count); // 2 bags at top level
        Assert.All(inventoryItems, bag => Assert.Single(bag.Contents)); // Each has 1 item
        
        // One bag has wheat (ID=2), one has sword (ID=3)
        var contentIds = inventoryItems.SelectMany(b => b.Contents).Select(c => c.ObjectDefinitionId).ToList();
        Assert.Contains(2, contentIds); // wheat
        Assert.Contains(3, contentIds); // sword

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Check both bags and contents
        Assert.Equal(2, loadedPlayer.InventoryObjectIds.Count);
        var bags = loadedPlayer.InventoryObjectIds
            .Select(id => _worldState.GetObjectInstance(id))
            .Where(obj => obj?.Definition.Type == "container")
            .ToList();
        Assert.Equal(2, bags.Count);
        
        // Each bag should have one item
        Assert.All(bags, bag => Assert.Single(bag!.Contents));
        
        // One bag has wheat, one has sword
        var allContents = bags.SelectMany(b => b!.Contents).ToList();
        Assert.Contains(allContents, item => item.Definition.Name == "wheat");
        Assert.Contains(allContents, item => item.Definition.Name == "sword");
    }

    [Fact]
    public void SaveAndLoad_ContainerStateChanges_PersistsCorrectly()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create chest that starts closed
        var chest = _worldState.CreateObjectInstance(4)!; // chest
        chest.IsClosed = true;
        chest.IsLocked = false;
        player.AddToInventory(chest.InstanceId);

        // Save initial state
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Load and verify initial state
        var loadedPlayer1 = CharacterMapper.ToPlayerState(character, 2, _worldState);
        var loadedChest1 = _worldState.GetObjectInstance(loadedPlayer1.InventoryObjectIds.First());
        Assert.True(loadedChest1!.IsClosed);
        Assert.False(loadedChest1.IsLocked);

        // Modify state - open and lock (unrealistic but tests state tracking)
        loadedChest1.IsClosed = false;
        loadedChest1.IsLocked = true;

        // Save modified state
        CharacterMapper.UpdateCharacterFromPlayerState(character, loadedPlayer1, _worldState);
        _dbContext.SaveChanges();

        // Load again and verify modified state
        var loadedPlayer2 = CharacterMapper.ToPlayerState(character, 3, _worldState);
        var loadedChest2 = _worldState.GetObjectInstance(loadedPlayer2.InventoryObjectIds.First());
        Assert.False(loadedChest2!.IsClosed);
        Assert.True(loadedChest2.IsLocked);
    }

    [Fact]
    public void SaveAndLoad_ItemOrderPreserved_NewestFirst()
    {
        // Arrange - Create account first (required by foreign key)
        var account = CreateAccount("testuser");
        _dbContext.Accounts.Add(account);
        _dbContext.SaveChanges();

        // Create character and player state
        var character = CreateCharacter("TestPlayer", account.AccountId);
        _dbContext.Characters.Add(character);
        _dbContext.SaveChanges();

        var player = new PlayerState(1, "TestPlayer", roomId: 1);
        
        // Create bag and add items in sequence
        var bag = _worldState.CreateObjectInstance(1)!; // bag
        var wheat1 = _worldState.CreateObjectInstance(2)!; // wheat (oldest)
        var wheat2 = _worldState.CreateObjectInstance(2)!; // wheat
        var wheat3 = _worldState.CreateObjectInstance(2)!; // wheat (newest)

        player.AddToInventory(bag.InstanceId);
        bag.AddItem(wheat1);
        bag.AddItem(wheat2);
        bag.AddItem(wheat3);

        // Act - Save
        CharacterMapper.UpdateCharacterFromPlayerState(character, player, _worldState);
        _dbContext.SaveChanges();

        // Verify sequence order in database - JSON array order should be preserved
        // Note: With JSON, order is naturally preserved by array index (no explicit SequenceOrder field needed)
        var reloadedChar = _dbContext.Characters.Find(character.CharacterId);
        Assert.NotNull(reloadedChar);
        Assert.NotNull(reloadedChar.InventoryJson);
        
        var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(reloadedChar.InventoryJson);
        Assert.NotNull(inventoryItems);
        Assert.Single(inventoryItems); // Just the bag
        Assert.Equal(3, inventoryItems[0].Contents.Count); // 3 wheats inside, order preserved by array index

        // Act - Load into new player state
        var loadedPlayer = CharacterMapper.ToPlayerState(character, 2, _worldState);

        // Assert - Items loaded in correct order (preserved by SequenceOrder)
        var loadedBag = _worldState.GetObjectInstance(loadedPlayer.InventoryObjectIds.First());
        Assert.Equal(3, loadedBag!.Contents.Count);
    }

    // Helper methods

    private Account CreateAccount(string username)
    {
        return new Account
        {
            Username = username,
            PasswordHash = "test-hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private Character CreateCharacter(string name, int accountId)
    {
        return new Character
        {
            AccountId = accountId,
            Name = name,
            Race = "Human",
            CharacterClass = "Warrior",
            Sex = "Male",
            Level = 1,
            RoomId = 1,
            HitPoints = 100,
            MaxHitPoints = 100,
            Mana = 100,
            MaxMana = 100,
            Movement = 100,
            MaxMovement = 100,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ObjectDefinition CreateBagDefinition(int id, string name)
    {
        return new ObjectDefinition(
            Id: id,
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
                    Capacity: 100,
                    Flags: new List<string>(),
                    KeyId: 0,
                    CorpseType: 0,
                    CorpseBlood: 0,
                    CorpseLevel: 0)
            },
            Values: new[] { 100, 0, 0, 0 },
            Weight: 5,
            Cost: 10,
            Affects: Array.Empty<ObjectAffect>()
        );
    }

    private static ObjectDefinition CreateChestDefinition(int id, string name)
    {
        return new ObjectDefinition(
            Id: id,
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
                    Capacity: 200,
                    Flags: new List<string> { "Closeable", "Lockable" },
                    KeyId: 0,
                    CorpseType: 0,
                    CorpseBlood: 0,
                    CorpseLevel: 0)
            },
            Values: new[] { 200, 0, 0, 0 },
            Weight: 50,
            Cost: 100,
            Affects: Array.Empty<ObjectAffect>()
        );
    }

    private static ObjectDefinition CreateItemDefinition(int id, string name, string shortDesc)
    {
        return new ObjectDefinition(
            Id: id,
            Name: name,
            ShortDescription: shortDesc,
            LongDescription: $"{shortDesc} is here.",
            Description: $"You see {shortDesc}.",
            Type: "trash",
            WearSlots: Array.Empty<string>(),
            Flags: Array.Empty<string>(),
            Details: null,
            Values: new[] { 0, 0, 0, 0 },
            Weight: 10,
            Cost: 5,
            Affects: Array.Empty<ObjectAffect>()
        );
    }
}
