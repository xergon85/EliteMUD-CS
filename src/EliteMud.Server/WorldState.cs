using EliteMud.Application.World;
using EliteMud.Game;
using MobInstance = EliteMud.Application.World.MobInstance;

namespace EliteMud.Server;

internal sealed class WorldState : IWorldState
{
    private readonly Dictionary<int, MobDefinition> _mobDefinitions;
    private readonly Dictionary<int, ObjectDefinition> _objectDefinitions;
    private readonly Dictionary<int, List<MobInstance>> _roomMobs;
    private readonly Dictionary<int, List<ObjectInstance>> _roomObjects;
    private readonly Dictionary<int, ObjectInstance> _objectInstances = new();
    private readonly IReadOnlyList<ZoneDefinition> _zones;
    private int _nextMobInstanceId;
    private int _nextObjectInstanceId;

    public WorldState(
        WorldDefinition world,
        Dictionary<int, MobDefinition> mobDefinitions,
        Dictionary<int, ObjectDefinition> objectDefinitions,
        Dictionary<int, List<MobInstance>> roomMobs,
        Dictionary<int, List<ObjectInstance>> roomObjects,
        IReadOnlyList<ZoneDefinition> zones)
    {
        World = world;
        _mobDefinitions = mobDefinitions;
        _objectDefinitions = objectDefinitions;
        _roomMobs = roomMobs;
        _roomObjects = roomObjects;
        _zones = zones;
    }

    public WorldDefinition World { get; }

    public IReadOnlyDictionary<int, MobDefinition> MobDefinitions => _mobDefinitions;

    public IReadOnlyDictionary<int, ObjectDefinition> ObjectDefinitions => _objectDefinitions;

    public IReadOnlyList<MobInstance> GetMobsInRoom(int roomId)
    {
        return _roomMobs.TryGetValue(roomId, out var mobs)
            ? mobs
            : Array.Empty<MobInstance>();
    }

    public IReadOnlyList<ObjectInstance> GetObjectsInRoom(int roomId)
    {
        return _roomObjects.TryGetValue(roomId, out var objects)
            ? objects
            : Array.Empty<ObjectInstance>();
    }

    public IReadOnlyList<ObjectInstance> GetPlayerInventory(PlayerState player)
    {
        var inventory = new List<ObjectInstance>();
        foreach (var objectId in player.InventoryObjectIds)
        {
            if (_objectInstances.TryGetValue(objectId, out var obj))
            {
                inventory.Add(obj);
            }
        }
        return inventory;
    }

    public IReadOnlyDictionary<EquipmentSlot, ObjectInstance> GetPlayerEquipment(PlayerState player)
    {
        var equipment = new Dictionary<EquipmentSlot, ObjectInstance>();
        foreach (var (slotInt, objectId) in player.EquipmentSlotToObjectId)
        {
            if (_objectInstances.TryGetValue(objectId, out var obj))
            {
                var slot = (EquipmentSlot)slotInt;
                equipment[slot] = obj;
            }
        }
        return equipment;
    }

    public ObjectInstance? GetObjectInstance(int instanceId)
    {
        return _objectInstances.TryGetValue(instanceId, out var obj) ? obj : null;
    }

    public bool TakeObject(PlayerState player, int objectInstanceId)
    {
        // Find the object in the room
        if (!_roomObjects.TryGetValue(player.RoomId, out var roomObjects))
        {
            return false;
        }

        var obj = roomObjects.FirstOrDefault(o => o.InstanceId == objectInstanceId);
        if (obj is null)
        {
            return false;
        }

        // Remove from room
        roomObjects.Remove(obj);

        // Add to player inventory
        player.AddToInventory(objectInstanceId);

        return true;
    }

    public bool DropObject(PlayerState player, int objectInstanceId)
    {
        // Check if player has the object
        if (!player.InventoryObjectIds.Contains(objectInstanceId))
        {
            return false;
        }

        // Get the object instance
        if (!_objectInstances.TryGetValue(objectInstanceId, out var obj))
        {
            return false;
        }

        // Remove from player inventory
        player.RemoveFromInventory(objectInstanceId);

        // Add to room
        if (!_roomObjects.TryGetValue(player.RoomId, out var roomObjects))
        {
            roomObjects = new List<ObjectInstance>();
            _roomObjects[player.RoomId] = roomObjects;
        }

        roomObjects.Add(obj);

        return true;
    }

    public bool EquipObject(PlayerState player, int objectInstanceId, EquipmentSlot slot)
    {
        // Check if player has the object in inventory
        if (!player.InventoryObjectIds.Contains(objectInstanceId))
        {
            return false;
        }

        // Get the object instance
        if (!_objectInstances.TryGetValue(objectInstanceId, out var obj))
        {
            return false;
        }

        // Check if object can be worn in this slot
        var slotName = slot.ToString();
        if (!obj.Definition.WearSlots.Contains(slotName))
        {
            return false;
        }

        // Try to equip to slot
        if (!player.EquipToSlot((int)slot, objectInstanceId))
        {
            return false; // Slot already occupied
        }

        // Remove from inventory
        player.RemoveFromInventory(objectInstanceId);

        return true;
    }

    public bool UnequipObject(PlayerState player, EquipmentSlot slot)
    {
        // Try to unequip from slot
        if (!player.UnequipFromSlot((int)slot, out var objectInstanceId))
        {
            return false; // Nothing equipped in that slot
        }

        // Add back to inventory
        player.AddToInventory(objectInstanceId);

        return true;
    }

    public void ResetAllZones()
    {
        foreach (var zone in _zones)
        {
            ResetZone(zone.Id);
        }
    }

    public bool ResetZoneForRoom(int roomId, out int zoneId)
    {
        foreach (var zone in _zones)
        {
            if (roomId >= zone.RoomRange.Min && roomId <= zone.RoomRange.Max)
            {
                zoneId = zone.Id;
                return ResetZone(zone.Id);
            }
        }

        zoneId = 0;
        return false;
    }

    public bool ResetZone(int zoneId)
    {
        var zone = FindZone(zoneId);
        if (zone is null)
        {
            return false;
        }

        ClearZoneRooms(zone);
        ApplyZoneResets(zone);
        return true;
    }

    private ZoneDefinition? FindZone(int zoneId)
    {
        foreach (var zone in _zones)
        {
            if (zone.Id == zoneId)
            {
                return zone;
            }
        }

        return null;
    }

    private void ClearZoneRooms(ZoneDefinition zone)
    {
        foreach (var roomId in _roomMobs.Keys)
        {
            if (roomId < zone.RoomRange.Min || roomId > zone.RoomRange.Max)
            {
                continue;
            }

            _roomMobs[roomId].Clear();
        }

        var objectDefsWithMaxLimit = new HashSet<int>();
        foreach (var reset in zone.ResetCommands)
        {
            if (string.Equals(reset.Type, "LoadObject", StringComparison.OrdinalIgnoreCase) 
                && reset.ObjectId.HasValue 
                && reset.MaxExisting.HasValue)
            {
                objectDefsWithMaxLimit.Add(reset.ObjectId.Value);
            }
        }

        foreach (var roomId in _roomObjects.Keys)
        {
            if (roomId < zone.RoomRange.Min || roomId > zone.RoomRange.Max)
            {
                continue;
            }

            if (objectDefsWithMaxLimit.Count == 0)
            {
                _roomObjects[roomId].Clear();
            }
            else
            {
                _roomObjects[roomId].RemoveAll(obj => !objectDefsWithMaxLimit.Contains(obj.Definition.Id));
            }
        }
    }

    private void ApplyZoneResets(ZoneDefinition zone)
    {
        MobInstance? lastMob = null;
        var random = new Random();

        foreach (var reset in zone.ResetCommands)
        {
            // Skip if IfFlag is set but no previous mob loaded
            if (reset.IfFlag && lastMob is null)
            {
                continue;
            }

            switch (reset.Type)
            {
                case "LoadMob":
                    lastMob = ExecuteLoadMob(reset);
                    break;

                case "LoadObject":
                    ExecuteLoadObject(reset, random);
                    break;

                case "EquipMob":
                    ExecuteEquipMob(reset, lastMob, random);
                    break;

                case "GiveMob":
                    ExecuteGiveMob(reset, lastMob, random);
                    break;

                case "PutObject":
                    ExecutePutObject(reset, random);
                    break;

                case "DoorState":
                    // TODO: Implement door state resets
                    break;

                case "RemoveObject":
                    // TODO: Implement object removal
                    break;
            }
        }
    }

    private MobInstance? ExecuteLoadMob(ZoneResetDefinition reset)
    {
        if (!reset.MobId.HasValue || !reset.RoomId.HasValue)
        {
            return null;
        }

        if (!_mobDefinitions.TryGetValue(reset.MobId.Value, out var mobDefinition))
        {
            return null;
        }

        if (!_roomMobs.TryGetValue(reset.RoomId.Value, out var list))
        {
            list = new List<MobInstance>();
            _roomMobs[reset.RoomId.Value] = list;
        }

        var desiredCount = Math.Max(1, reset.MaxExisting ?? 1);
        var existing = 0;
        foreach (var instance in list)
        {
            if (instance.Definition.Id == mobDefinition.Id)
            {
                existing++;
            }
        }

        if (existing >= desiredCount)
        {
            return null;
        }

        var mob = new MobInstance(_nextMobInstanceId++, mobDefinition);
        list.Add(mob);
        return mob;
    }

    private void ExecuteLoadObject(ZoneResetDefinition reset, Random random)
    {
        if (!reset.ObjectId.HasValue || !reset.RoomId.HasValue)
        {
            return;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return;
        }

        if (!_objectDefinitions.TryGetValue(reset.ObjectId.Value, out var objectDefinition))
        {
            return;
        }

        if (!_roomObjects.TryGetValue(reset.RoomId.Value, out var list))
        {
            list = new List<ObjectInstance>();
            _roomObjects[reset.RoomId.Value] = list;
        }

        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        _objectInstances[objectInstance.InstanceId] = objectInstance;
        list.Add(objectInstance);
    }

    private void ExecuteEquipMob(ZoneResetDefinition reset, MobInstance? mob, Random random)
    {
        if (mob is null || !reset.ObjectId.HasValue || !reset.EquipSlot.HasValue)
        {
            return;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return;
        }

        if (!_objectDefinitions.TryGetValue(reset.ObjectId.Value, out var objectDefinition))
        {
            return;
        }

        var slot = (EquipmentSlot)reset.EquipSlot.Value;
        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        _objectInstances[objectInstance.InstanceId] = objectInstance;
        mob.Equip(objectInstance, slot);
    }

    private void ExecuteGiveMob(ZoneResetDefinition reset, MobInstance? mob, Random random)
    {
        if (mob is null || !reset.ObjectId.HasValue)
        {
            return;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return;
        }

        // TODO: Implement mob inventory storage
        // For now, this is a placeholder - need to add inventory to MobInstance
    }

    private void ExecutePutObject(ZoneResetDefinition reset, Random random)
    {
        if (!reset.ObjectId.HasValue || !reset.ContainerId.HasValue)
        {
            return;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return;
        }

        // TODO: Implement container storage
        // For now, this is a placeholder - need to track object containers
    }

    private static bool CheckSpawnChance(int? spawnChance, Random random)
    {
        if (!spawnChance.HasValue)
        {
            return true; // No spawn chance = always spawn
        }

        // Legacy logic: spawnChance >= random(1, 100)
        // This means spawnChance=100 is 100%, spawnChance=1 is 1%
        var roll = random.Next(1, 101); // 1-100 inclusive
        return spawnChance.Value >= roll;
    }
}
