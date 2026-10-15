using EliteMud.Application.Commands.Drop;
using EliteMud.Application.Commands.Put;
using EliteMud.Application.Commands.Remove;
using EliteMud.Application.Commands.Wear;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Commands;

/// <summary>
/// Players and mobs hold the items they carry and wear (#80). The "all" commands move items out of the very list
/// they go through, so each must work from a copy of it.
/// </summary>
public class CarriedItemsTests
{
    private const int Room = 1;

    private readonly WorldState _world;
    private readonly PlayerState _player = new(1, "Tester", Room);
    private readonly ObjectInstance _helmet;
    private readonly ObjectInstance _boots;
    private readonly ObjectInstance _sword;
    private readonly ObjectInstance _bag;

    public CarriedItemsTests()
    {
        _helmet = Item(10, "helmet", "Head");
        _boots = Item(11, "boots", "Feet");
        _sword = Item(12, "sword");
        _bag = Bag(13);
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Room] = new(Room, "Room", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Room] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
            new List<ZoneDefinition>());
    }

    [Fact]
    public void DropAll_DropsEverythingCarried()
    {
        Carry(_helmet, _boots, _sword);

        Assert.True(new DropHandler(_world).Handle(_player, "all").Success);

        Assert.Empty(_player.Inventory);
        Assert.Equal(new[] { _helmet, _boots, _sword }, _world.GetObjectsInRoom(Room));
    }

    [Fact]
    public void WearAll_WearsEverythingWearable_AndKeepsTheRest()
    {
        Carry(_helmet, _boots, _sword);

        Assert.True(new WearHandler(_world).Handle(_player, "all").Success);

        Assert.Same(_helmet, _player.Equipment[EquipmentSlot.Head]);
        Assert.Same(_boots, _player.Equipment[EquipmentSlot.Feet]);
        Assert.Equal(new[] { _sword }, _player.Inventory);
    }

    [Fact]
    public void RemoveAll_PutsEverythingWornBackInTheInventory()
    {
        _player.Equip(_helmet, EquipmentSlot.Head);
        _player.Equip(_boots, EquipmentSlot.Feet);

        Assert.True(new RemoveHandler(_world).Handle(_player, "all").Success);

        Assert.Empty(_player.Equipment);
        Assert.Equal(2, _player.Inventory.Count);
        Assert.Contains(_helmet, _player.Inventory);
        Assert.Contains(_boots, _player.Inventory);
    }

    [Fact]
    public void PutAll_PutsEverythingElseInTheBag()
    {
        Carry(_bag, _helmet, _sword);

        Assert.True(new PutHandler(_world).Handle(_player, "all bag").Success);

        Assert.Equal(new[] { _bag }, _player.Inventory);
        Assert.Equal(new[] { _helmet, _sword }, _bag.Contents);
    }

    [Fact]
    public void InventoryWithContents_ReachesIntoNestedContainers()
    {
        var pouch = Bag(14);
        pouch.AddItem(_sword);
        _bag.AddItem(pouch);
        Carry(_bag, _helmet);

        Assert.Equal(new[] { _bag, pouch, _sword, _helmet }, _player.InventoryWithContents());
        Assert.Equal(new[] { _bag, _helmet }, _player.Inventory);
    }

    /// <summary>Legacy make_corpse (fight.c:340, 371–377): what a player carries and wears goes into the corpse.</summary>
    [Fact]
    public void APlayersCorpse_GetsWhatTheyCarriedAndWore()
    {
        Carry(_sword);
        _player.Equip(_helmet, EquipmentSlot.Head);

        var corpse = _world.CreatePlayerCorpse(_player, Room);

        Assert.Equal(new[] { _sword, _helmet }, corpse.Contents);
        Assert.Empty(_player.Inventory);
        Assert.Empty(_player.Equipment);
    }

    [Fact]
    public void AMobsCorpse_GetsWhatItCarried()
    {
        var mob = new MobInstance(1, new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 1, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null));
        mob.AddToInventory(_sword);
        mob.Equip(_helmet, EquipmentSlot.Head);

        var corpse = _world.CreateMobCorpse(mob, Room);

        Assert.Equal(new[] { _helmet, _sword }, corpse.Contents);
    }

    private void Carry(params ObjectInstance[] items)
    {
        foreach (var item in items)
        {
            _player.AddToInventory(item);
        }
    }

    private static ObjectInstance Item(int id, string name, params string[] wearSlots) =>
        new(id, new ObjectDefinition(
            id, name, $"a {name}", $"A {name} is here.", "Desc", "Armor",
            new List<string>(wearSlots) { "Take" }, new List<string>(), null, new List<int>(), 1, 10,
            new List<ObjectAffect>()));

    private static ObjectInstance Bag(int id) =>
        new(id, new ObjectDefinition(
            id, "bag", "a bag", "A bag is here.", "Desc", "container",
            new List<string> { "Take" }, new List<string>(),
            new ObjectDetails { Container = new ObjectContainer(100, new List<string>(), 0, 0, 0, 0) },
            new List<int> { 100, 0, 0, 0 }, 5, 10, new List<ObjectAffect>()));
}
