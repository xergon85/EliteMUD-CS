using EliteMud.Game;

namespace EliteMud.Application.World;

public sealed class WorldState : IWorldState
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

        // NOTE: We don't check WearSlots here because the command handlers (like HoldHandler)
        // have already validated compatibility. For example, 'hold' accepts items with either
        // Hold OR Wield flags (legacy: wear_bitvectors[HOLD] = ITEM_HOLD | ITEM_WIELD), so
        // we can't do a simple slot name match here.

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

    public ObjectInstance? LoadObjectToPlayer(PlayerState player, int objectDefinitionId)
    {
        // Check if object definition exists
        if (!_objectDefinitions.TryGetValue(objectDefinitionId, out var objectDefinition))
        {
            return null;
        }

        // Create new object instance
        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        _objectInstances[objectInstance.InstanceId] = objectInstance;

        // Add to player inventory
        player.AddToInventory(objectInstance.InstanceId);

        return objectInstance;
    }

    public ObjectInstance? CreateObjectInstance(int objectDefinitionId)
    {
        // Check if object definition exists
        if (!_objectDefinitions.TryGetValue(objectDefinitionId, out var objectDefinition))
        {
            return null;
        }

        // Create new object instance
        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        _objectInstances[objectInstance.InstanceId] = objectInstance;

        return objectInstance;
    }

    public IReadOnlyList<ObjectDefinition> SearchObjects(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ObjectDefinition>();
        }

        var queryLower = query.ToLowerInvariant();
        var results = new List<ObjectDefinition>();

        foreach (var objDef in _objectDefinitions.Values)
        {
            // Search in Name and ShortDescription
            if (objDef.Name?.ToLowerInvariant().Contains(queryLower) == true ||
                objDef.ShortDescription?.ToLowerInvariant().Contains(queryLower) == true)
            {
                results.Add(objDef);
            }
        }

        return results;
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

    /// <summary>
    /// Create a player corpse with all inventory and equipment transferred.
    /// Legacy: make_corpse() in fight.c:310-393
    /// </summary>
    public ObjectInstance CreatePlayerCorpse(PlayerState player, int roomId)
    {
        // Create corpse description based on HP (fight.c:326-335)
        string description;
        if (player.HitPoints > -20)
            description = $"The corpse of {player.Name} is lying here.";
        else if (player.HitPoints > -40)
            description = $"The corpse of {player.Name} is lying here, looking mutilated.";
        else if (player.HitPoints > -80)
            description = $"The corpse of {player.Name} is lying here, or rather parts of it.";
        else
            description = "The remains of something or someone is lying here.";

        // Calculate total weight (player weight + items)
        int totalWeight = 100; // Base player weight
        foreach (var objectId in player.InventoryObjectIds)
        {
            if (_objectInstances.TryGetValue(objectId, out var obj))
            {
                totalWeight += obj.Definition.Weight;
            }
        }
        foreach (var objectId in player.EquipmentSlotToObjectId.Values)
        {
            if (_objectInstances.TryGetValue(objectId, out var obj))
            {
                totalWeight += obj.Definition.Weight;
            }
        }

        // Create corpse object definition
        var corpseDefinition = new ObjectDefinition(
            Id: -1, // Dynamic object (not from content files)
            Name: "corpse pcorpse", // Player corpse identifier
            ShortDescription: $"the corpse of {player.Name}",
            LongDescription: description,
            Description: description,
            Type: "container",
            WearSlots: new List<string> { "take" },
            Flags: new List<string> { "nodonate", "nosweep" },
            Details: null,
            Values: new List<int> { 0, 0, 0, 2 }, // value[3]=2 for player corpse identifier
            Weight: totalWeight,
            Cost: player.Level * 50
        );

        // Create corpse instance
        var corpse = new ObjectInstance(_nextObjectInstanceId++, corpseDefinition);
        _objectInstances[corpse.InstanceId] = corpse;

        // Transfer inventory items to corpse (fight.c:340)
        foreach (var objectId in player.InventoryObjectIds.ToList())
        {
            if (_objectInstances.TryGetValue(objectId, out var obj))
            {
                corpse.AddItem(obj);
            }
        }

        // Transfer equipment to corpse (fight.c:371-373)
        foreach (var slotId in player.EquipmentSlotToObjectId.Keys.ToList())
        {
            if (player.UnequipFromSlot(slotId, out var objectId))
            {
                if (_objectInstances.TryGetValue(objectId, out var obj))
                {
                    corpse.AddItem(obj);
                }
            }
        }

        // Transfer gold as money object (fight.c:341-347)
        if (player.Gold > 0)
        {
            var money = CreateMoneyObject(player.Gold);
            corpse.AddItem(money);
            player.Gold = 0;
        }

        // Clear player inventory (fight.c:375-377)
        while (player.InventoryObjectIds.Count > 0)
        {
            player.RemoveFromInventory(player.InventoryObjectIds[0]);
        }

        // Place corpse in room (fight.c:392)
        if (!_roomObjects.TryGetValue(roomId, out var roomObjects))
        {
            roomObjects = new List<ObjectInstance>();
            _roomObjects[roomId] = roomObjects;
        }
        roomObjects.Add(corpse);

        return corpse;
    }

    /// <summary>
    /// Create a mob corpse with all equipment transferred.
    /// Legacy: make_corpse() in fight.c:310-393 for NPCs
    /// </summary>
    public ObjectInstance CreateMobCorpse(MobInstance mob, int roomId)
    {
        // Clean mob short description (remove any newlines/whitespace)
        var mobShortDesc = mob.Definition.ShortDescription?.Trim().Replace("\n", " ").Replace("\r", " ") ?? "someone";
        
        // Create corpse description
        string description = $"The corpse of {mobShortDesc} is lying here.";

        // Calculate total weight (mob weight + equipment)
        int totalWeight = 100; // Base mob weight
        foreach (var obj in mob.Equipment.Values)
        {
            totalWeight += obj.Definition.Weight;
        }

        // Create corpse object definition
        var corpseDefinition = new ObjectDefinition(
            Id: -1, // Dynamic object
            Name: "corpse",
            ShortDescription: $"the corpse of {mobShortDesc}",
            LongDescription: description,
            Description: description,
            Type: "container",
            WearSlots: new List<string> { "take" },
            Flags: new List<string> { "nodonate", "nosweep" },
            Details: null,
            Values: new List<int> { 0, 0, 0, 1 }, // value[3]=1 for NPC corpse identifier
            Weight: totalWeight,
            Cost: mob.Definition.Level * 50
        );

        // Create corpse instance
        var corpse = new ObjectInstance(_nextObjectInstanceId++, corpseDefinition);
        _objectInstances[corpse.InstanceId] = corpse;

        // Transfer mob equipment to corpse (fight.c:371-373)
        foreach (var slot in mob.Equipment.Keys.ToList())
        {
            var obj = mob.Unequip(slot);
            if (obj is not null)
            {
                corpse.AddItem(obj);
            }
        }

        // Transfer mob gold as money object (fight.c:341-347)
        // Mobs don't have a Gold property in current implementation, but we can add it later
        // For now, skip gold transfer for mobs

        // Place corpse in room (fight.c:392)
        if (!_roomObjects.TryGetValue(roomId, out var roomObjects))
        {
            roomObjects = new List<ObjectInstance>();
            _roomObjects[roomId] = roomObjects;
        }
        roomObjects.Add(corpse);

        return corpse;
    }

    /// <summary>
    /// Create a money object with the specified amount of gold.
    /// Legacy: create_money() in fight.c:318
    /// </summary>
    private ObjectInstance CreateMoneyObject(int amount)
    {
        var moneyDefinition = new ObjectDefinition(
            Id: -2, // Special dynamic object for money
            Name: $"gold coin{(amount == 1 ? "" : "s")}",
            ShortDescription: $"{amount} gold coin{(amount == 1 ? "" : "s")}",
            LongDescription: $"{amount} gold coin{(amount == 1 ? "" : "s")} is here.",
            Description: "A pile of gold coins.",
            Type: "money",
            WearSlots: new List<string> { "take" },
            Flags: new List<string>(),
            Details: null,
            Values: new List<int> { amount, 0, 0, 0 }, // value[0] = gold amount
            Weight: Math.Max(1, amount / 10), // 10 coins = 1 weight unit
            Cost: amount
        );

        var money = new ObjectInstance(_nextObjectInstanceId++, moneyDefinition);
        _objectInstances[money.InstanceId] = money;
        return money;
    }

    /// <summary>
    /// Remove a mob from the world completely.
    /// Legacy: extract_char() in handler.c
    /// </summary>
    public bool RemoveMob(int mobInstanceId, int roomId)
    {
        if (!_roomMobs.TryGetValue(roomId, out var mobs))
        {
            return false;
        }

        var mob = mobs.FirstOrDefault(m => m.InstanceId == mobInstanceId);
        if (mob is null)
        {
            return false;
        }

        mobs.Remove(mob);
        return true;
    }
}
