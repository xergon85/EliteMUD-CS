using EliteMud.Game;

namespace EliteMud.Application.World;

/// <summary>
/// Runtime state for a door.
/// Doors are bidirectional - opening/closing/locking one side affects both sides.
/// </summary>
public sealed class DoorState
{
    public bool IsClosed { get; set; }
    public bool IsLocked { get; set; }
    
    /// <summary>
    /// Indicates if the door has been broken by the bash command.
    /// Legacy: EX_BROKEN flag
    /// Once broken, the door cannot be closed or locked again.
    /// </summary>
    public bool IsBroken { get; set; }
}

public sealed class WorldState : IWorldState
{
    private readonly Dictionary<int, MobDefinition> _mobDefinitions;
    private readonly Dictionary<int, ObjectDefinition> _objectDefinitions;
    private readonly Dictionary<int, List<ObjectInstance>> _roomObjects;
    private readonly IReadOnlyList<ZoneDefinition> _zones;
    private readonly Dictionary<(int roomId, Direction direction), DoorState> _doorStates = new();
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
        _roomObjects = roomObjects;
        _zones = zones;
        Population = new WorldPopulation(world);

        foreach (var (roomId, mobs) in roomMobs)
        {
            foreach (var mob in mobs)
            {
                Population.Place(mob, roomId);
            }
        }
        
        // Start new instance ids after the room objects', to avoid collisions
        foreach (var (roomId, objects) in roomObjects)
        {
            foreach (var obj in objects)
            {
                if (obj.InstanceId >= _nextObjectInstanceId)
                {
                    _nextObjectInstanceId = obj.InstanceId + 1;
                }
            }
        }
        
        // Initialize door states from room definitions. Closed exits that aren't doors (barriers, e.g.
        // the Desert Moongate) get a state too: legacy blocks movement on EX_CLOSED alone.
        foreach (var (roomId, room) in world.Rooms)
        {
            foreach (var exit in room.Exits)
            {
                if (exit.IsDoor || exit.InitiallyClosed)
                {
                    var key = (roomId, exit.Direction);
                    if (!_doorStates.ContainsKey(key))
                    {
                        _doorStates[key] = new DoorState
                        {
                            IsClosed = exit.InitiallyClosed,
                            IsLocked = exit.InitiallyLocked
                        };
                    }
                }
            }
        }
    }

    public WorldDefinition World { get; }

    public WorldPopulation Population { get; }

    public IReadOnlyDictionary<int, MobDefinition> MobDefinitions => _mobDefinitions;

    public IReadOnlyDictionary<int, ObjectDefinition> ObjectDefinitions => _objectDefinitions;

    public IReadOnlyList<ObjectInstance> GetObjectsInRoom(int roomId)
    {
        return _roomObjects.TryGetValue(roomId, out var objects)
            ? objects
            : Array.Empty<ObjectInstance>();
    }

    public IReadOnlyList<(int RoomId, ObjectInstance Portal)> TickPortals()
    {
        var closed = new List<(int RoomId, ObjectInstance Portal)>();
        foreach (var (roomId, objects) in _roomObjects)
        {
            foreach (var obj in objects)
            {
                if (obj.Portal?.TickHour() == true)
                {
                    closed.Add((roomId, obj));
                }
            }
        }

        return closed;
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
        player.AddToInventory(obj);

        return true;
    }

    public bool DropObject(PlayerState player, int objectInstanceId)
    {
        // Check if player has the object
        var obj = player.Inventory.FirstOrDefault(o => o.InstanceId == objectInstanceId);
        if (obj is null)
        {
            return false;
        }

        // Remove from player inventory
        player.RemoveFromInventory(obj);

        // Add to room
        if (!_roomObjects.TryGetValue(player.RoomId, out var roomObjects))
        {
            roomObjects = new List<ObjectInstance>();
            _roomObjects[player.RoomId] = roomObjects;
        }

        roomObjects.Add(obj);

        return true;
    }

    public bool TakeObjectForMob(MobInstance mob, int objectInstanceId, int roomId)
    {
        // Find the object in the room
        if (!_roomObjects.TryGetValue(roomId, out var roomObjects))
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

        // Add to mob inventory
        mob.AddToInventory(obj);

        return true;
    }

    public bool DropObjectForMob(MobInstance mob, int objectInstanceId, int roomId)
    {
        // Check if mob has the object
        var obj = mob.Inventory.FirstOrDefault(o => o.InstanceId == objectInstanceId);
        if (obj is null)
        {
            return false;
        }

        // Remove from mob inventory
        mob.RemoveFromInventory(obj);

        // Add to room
        if (!_roomObjects.TryGetValue(roomId, out var roomObjects))
        {
            roomObjects = new List<ObjectInstance>();
            _roomObjects[roomId] = roomObjects;
        }

        roomObjects.Add(obj);

        return true;
    }

    public bool EquipObject(PlayerState player, int objectInstanceId, EquipmentSlot slot)
    {
        // Check if player has the object in inventory
        var obj = player.Inventory.FirstOrDefault(o => o.InstanceId == objectInstanceId);
        if (obj is null)
        {
            return false;
        }

        // NOTE: We don't check WearSlots here because the command handlers (like HoldHandler)
        // have already validated compatibility. For example, 'hold' accepts items with either
        // Hold OR Wield flags (legacy: wear_bitvectors[HOLD] = ITEM_HOLD | ITEM_WIELD), so
        // we can't do a simple slot name match here.

        // Try to equip to slot
        if (!player.Equip(obj, slot))
        {
            return false; // Slot already occupied
        }

        // Remove from inventory
        player.RemoveFromInventory(obj);

        return true;
    }

    public bool UnequipObject(PlayerState player, EquipmentSlot slot)
    {
        // Try to unequip from slot
        if (player.Unequip(slot) is not { } obj)
        {
            return false; // Nothing equipped in that slot
        }

        // Add back to inventory
        player.AddToInventory(obj);

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

        // Add to player inventory
        player.AddToInventory(objectInstance);

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
        var zoneMobs = Population.Mobs
            .Where(mob => mob.RoomId >= zone.RoomRange.Min && mob.RoomId <= zone.RoomRange.Max)
            .ToList();
        foreach (var mob in zoneMobs)
        {
            Population.Leave(mob);
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

        // Legacy: reset_zone() in db.c:2020 tracks last_cmd. A command with the if-flag set only
        // runs when the previous command succeeded.
        var lastCommandSucceeded = false;

        foreach (var reset in zone.ResetCommands)
        {
            if (reset.IfFlag && !lastCommandSucceeded)
            {
                continue;
            }

            switch (reset.Type)
            {
                case "LoadMob":
                    lastMob = ExecuteLoadMob(reset);
                    lastCommandSucceeded = lastMob is not null;
                    break;

                case "LoadObject":
                    lastCommandSucceeded = ExecuteLoadObject(reset, random);
                    break;

                case "EquipMob":
                    lastCommandSucceeded = ExecuteEquipMob(reset, lastMob, random);
                    break;

                case "GiveMob":
                    lastCommandSucceeded = ExecuteGiveMob(reset, lastMob, random);
                    break;

                case "PutObject":
                    lastCommandSucceeded = ExecutePutObject(reset, random);
                    break;

                case "LoadPortal":
                    lastCommandSucceeded = ExecuteLoadPortal(reset);
                    break;

                case "ActivatePortal":
                    lastCommandSucceeded = ExecuteActivatePortal(reset);
                    break;

                case "DoorState":
                    lastCommandSucceeded = ExecuteDoorState(reset);
                    break;

                case "ShuffleExits":
                    // Legacy doesn't update last_cmd for 'R'
                    ExecuteShuffleExits(reset, random);
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

        if (!World.Rooms.ContainsKey(reset.RoomId.Value))
        {
            return null;
        }

        var desiredCount = Math.Max(1, reset.MaxExisting ?? 1);
        var existing = 0;
        foreach (var instance in Population.MobsIn(reset.RoomId.Value))
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
        Population.Enter(mob, reset.RoomId.Value);
        return mob;
    }

    private bool ExecuteLoadObject(ZoneResetDefinition reset, Random random)
    {
        if (!reset.ObjectId.HasValue || !reset.RoomId.HasValue)
        {
            return false;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return false;
        }

        if (!_objectDefinitions.TryGetValue(reset.ObjectId.Value, out var objectDefinition))
        {
            return false;
        }

        if (!_roomObjects.TryGetValue(reset.RoomId.Value, out var list))
        {
            list = new List<ObjectInstance>();
            _roomObjects[reset.RoomId.Value] = list;
        }

        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        list.Add(objectInstance);
        return true;
    }

    private bool ExecuteEquipMob(ZoneResetDefinition reset, MobInstance? mob, Random random)
    {
        if (mob is null || !reset.ObjectId.HasValue || !reset.EquipSlot.HasValue)
        {
            return false;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return false;
        }

        if (!_objectDefinitions.TryGetValue(reset.ObjectId.Value, out var objectDefinition))
        {
            return false;
        }

        var slot = (EquipmentSlot)reset.EquipSlot.Value;
        var objectInstance = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
        mob.Equip(objectInstance, slot);
        return true;
    }

    private bool ExecuteGiveMob(ZoneResetDefinition reset, MobInstance? mob, Random random)
    {
        if (mob is null || !reset.ObjectId.HasValue)
        {
            return false;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return false;
        }

        // TODO: Implement mob inventory storage
        // For now, this is a placeholder - need to add inventory to MobInstance
        // Reports success like legacy would, so if-flag chains behave the same.
        return true;
    }

    private bool ExecutePutObject(ZoneResetDefinition reset, Random random)
    {
        if (!reset.ObjectId.HasValue || !reset.ContainerId.HasValue)
        {
            return false;
        }

        if (!CheckSpawnChance(reset.SpawnChance, random))
        {
            return false;
        }

        // TODO: Implement container storage
        // For now, this is a placeholder - need to track object containers
        // Reports success like legacy would, so if-flag chains behave the same.
        return true;
    }

    /// <summary>
    /// Load a portal into a room, or reuse the one already there, and set its destination.
    /// Legacy: 'L' in reset_zone(), db.c:2185
    /// </summary>
    private bool ExecuteLoadPortal(ZoneResetDefinition reset)
    {
        if (!reset.RoomId.HasValue || !reset.ObjectId.HasValue || !reset.PortalDestination.HasValue
            || reset.RoomId.Value < 0 || reset.ObjectId.Value <= 0 || reset.PortalDestination.Value < 0)
        {
            return false;
        }

        if (!_objectDefinitions.TryGetValue(reset.ObjectId.Value, out var objectDefinition))
        {
            return false;
        }

        if (!_roomObjects.TryGetValue(reset.RoomId.Value, out var list))
        {
            list = new List<ObjectInstance>();
            _roomObjects[reset.RoomId.Value] = list;
        }

        var portal = list.FirstOrDefault(obj => obj.Portal is not null && obj.Definition.Id == objectDefinition.Id);
        if (portal is null)
        {
            portal = new ObjectInstance(_nextObjectInstanceId++, objectDefinition);
            if (portal.Portal is null)
            {
                return false;
            }

            list.Add(portal);
        }

        portal.Portal!.Destination = reset.PortalDestination.Value;
        return true;
    }

    /// <summary>
    /// Set the state flags and key of the first portal in a room.
    /// Legacy: 'A' in reset_zone(), db.c:2209
    /// </summary>
    private bool ExecuteActivatePortal(ZoneResetDefinition reset)
    {
        if (!reset.RoomId.HasValue || !_roomObjects.TryGetValue(reset.RoomId.Value, out var list))
        {
            return false;
        }

        var portal = list.FirstOrDefault(obj => obj.Portal is not null)?.Portal;
        if (portal is null)
        {
            return false;
        }

        portal.Flags = Math.Max(0, reset.PortalFlags ?? 0);
        portal.KeyObjectId = reset.PortalKeyId is > 0 ? reset.PortalKeyId.Value : -1;
        return true;
    }

    /// <summary>
    /// Shuffle the exits in a room's first N directions (north, east, south, west, up, down).
    /// Whole exits move, including their doors. Legacy: 'R' in reset_zone(), db.c:2176
    /// </summary>
    private void ExecuteShuffleExits(ZoneResetDefinition reset, Random random)
    {
        if (!reset.RoomId.HasValue || !reset.ExitCount.HasValue
            || !World.Rooms.TryGetValue(reset.RoomId.Value, out var room))
        {
            return;
        }

        var roomId = room.Id;
        var directionCount = Enum.GetValues<Direction>().Length;
        var count = Math.Min(reset.ExitCount.Value, directionCount);

        var slots = new ExitDefinition?[directionCount];
        foreach (var exit in room.Exits)
        {
            slots[(int)exit.Direction] = exit;
        }

        for (var i = 0; i < count - 1; i++)
        {
            var j = random.Next(i, count); // legacy number(i, count - 1), inclusive
            (slots[i], slots[j]) = (slots[j], slots[i]);
            SwapDoorStates(roomId, (Direction)i, (Direction)j);
        }

        var exits = new List<ExitDefinition>();
        for (var direction = 0; direction < directionCount; direction++)
        {
            if (slots[direction] is { } exit)
            {
                exits.Add(exit with { Direction = (Direction)direction });
            }
        }

        World.ReplaceRoomExits(roomId, exits);
    }

    private void SwapDoorStates(int roomId, Direction first, Direction second)
    {
        var firstKey = (roomId, first);
        var secondKey = (roomId, second);
        _doorStates.Remove(firstKey, out var firstState);
        _doorStates.Remove(secondKey, out var secondState);

        if (firstState is not null)
        {
            _doorStates[secondKey] = firstState;
        }

        if (secondState is not null)
        {
            _doorStates[firstKey] = secondState;
        }
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
        foreach (var obj in player.Inventory)
        {
            totalWeight += obj.Definition.Weight;
        }
        foreach (var obj in player.Equipment.Values)
        {
            totalWeight += obj.Definition.Weight;
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
            Cost: player.Level * 50,
            Affects: new List<ObjectAffect>()
        );

        // Create corpse instance
        var corpse = new ObjectInstance(_nextObjectInstanceId++, corpseDefinition);

        // Transfer inventory items to corpse (fight.c:340)
        foreach (var obj in player.Inventory)
        {
            corpse.AddItem(obj);
        }

        // Transfer equipment to corpse (fight.c:371-373)
        foreach (var slot in player.Equipment.Keys.ToList())
        {
            if (player.Unequip(slot) is { } obj)
            {
                corpse.AddItem(obj);
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
        while (player.Inventory.Count > 0)
        {
            player.RemoveFromInventory(player.Inventory[0]);
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
            Cost: mob.Definition.Level * 50,
            Affects: new List<ObjectAffect>()
        );

        // Create corpse instance
        var corpse = new ObjectInstance(_nextObjectInstanceId++, corpseDefinition);

        // Transfer mob equipment to corpse (fight.c:371-373)
        foreach (var slot in mob.Equipment.Keys.ToList())
        {
            var obj = mob.Unequip(slot);
            if (obj is not null)
            {
                corpse.AddItem(obj);
            }
        }

        // Transfer mob inventory to corpse (loot from scavenged items)
        foreach (var obj in mob.Inventory)
        {
            corpse.AddItem(obj);
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
            Cost: amount,
            Affects: new List<ObjectAffect>()
        );

        var money = new ObjectInstance(_nextObjectInstanceId++, moneyDefinition);
        return money;
    }

    // ===== Door State Management =====
    
    /// <summary>
    /// True if the exit is closed: a closed door, or a closed exit that isn't a door (a barrier).
    /// Legacy blocks movement on EX_CLOSED alone (do_simple_move() in act.movement.c).
    /// </summary>
    public bool IsExitClosed(int roomId, Direction direction) =>
        _doorStates.TryGetValue((roomId, direction), out var state) && state.IsClosed;

    /// <summary>
    /// Set the closed/locked state of one side of an exit: 0 open, 1 closed, 2 closed and locked.
    /// Works on any exit, door or not, and also repairs a bashed door. The other side is set by its own
    /// reset line, as in the zone files. Legacy: 'D' in reset_zone(), db.c:2143
    /// </summary>
    private bool ExecuteDoorState(ZoneResetDefinition reset)
    {
        if (!reset.RoomId.HasValue || !reset.DoorDirection.HasValue
            || !World.Rooms.TryGetValue(reset.RoomId.Value, out var room))
        {
            return false;
        }

        var direction = (Direction)reset.DoorDirection.Value;
        if (room.Exits.All(exit => exit.Direction != direction))
        {
            return false; // legacy logs "non-existant direction"
        }

        var key = (room.Id, direction);
        if (!_doorStates.TryGetValue(key, out var state))
        {
            state = new DoorState();
            _doorStates[key] = state;
        }

        state.IsClosed = reset.DoorState is 1 or 2;
        state.IsLocked = reset.DoorState is 2;
        state.IsBroken = false;
        return true;
    }

    /// <summary>
    /// Get the runtime closed/locked state of an exit.
    /// Doors always have one. A closed exit that isn't a door (a barrier, e.g. the Desert Moongate) has one
    /// too, since legacy keeps EX_CLOSED/EX_LOCKED on any exit. Returns null for exits that can't be closed.
    /// Legacy: EXIT(ch, door)->exit_info flags (EX_CLOSED, EX_LOCKED)
    /// </summary>
    public DoorState? GetDoorState(int roomId, Direction direction)
    {
        var key = (roomId, direction);
        return _doorStates.TryGetValue(key, out var state) ? state : null;
    }
    
    /// <summary>
    /// Set door state (open/close, lock/unlock) for both sides of the door.
    /// Doors are bidirectional - changing one side changes both.
    /// Legacy: OPEN_DOOR/CLOSE_DOOR/LOCK_DOOR/UNLOCK_DOOR macros in act.movement.c
    /// </summary>
    public void SetDoorState(int roomId, Direction direction, bool isClosed, bool isLocked)
    {
        var room = World.Rooms.GetValueOrDefault(roomId);
        if (room == null) return;
        
        var exit = room.Exits.FirstOrDefault(e => e.Direction == direction);
        if (exit == null || !exit.IsDoor) return;
        
        // Set state for this side
        var key = (roomId, direction);
        if (_doorStates.TryGetValue(key, out var state))
        {
            state.IsClosed = isClosed;
            state.IsLocked = isLocked;
        }
        
        // Set state for reverse side (bidirectional doors)
        var reverseDirection = GetReverseDirection(direction);
        var targetRoom = World.Rooms.GetValueOrDefault(exit.TargetRoomId);
        if (targetRoom != null)
        {
            var reverseExit = targetRoom.Exits.FirstOrDefault(e => e.Direction == reverseDirection && e.TargetRoomId == roomId);
            if (reverseExit != null && reverseExit.IsDoor)
            {
                var reverseKey = (exit.TargetRoomId, reverseDirection);
                if (_doorStates.TryGetValue(reverseKey, out var reverseState))
                {
                    reverseState.IsClosed = isClosed;
                    reverseState.IsLocked = isLocked;
                }
            }
        }
    }
    
    public void BreakDoor(int roomId, Direction direction)
    {
        var room = World.Rooms.GetValueOrDefault(roomId);
        if (room == null) return;
        
        var exit = room.Exits.FirstOrDefault(e => e.Direction == direction);
        if (exit == null || !exit.IsDoor) return;
        
        // Break this side: unlock, open, and mark as broken
        var key = (roomId, direction);
        if (_doorStates.TryGetValue(key, out var state))
        {
            state.IsClosed = false;
            state.IsLocked = false;
            state.IsBroken = true;
        }
        
        // Break reverse side (bidirectional doors)
        var reverseDirection = GetReverseDirection(direction);
        var targetRoom = World.Rooms.GetValueOrDefault(exit.TargetRoomId);
        if (targetRoom != null)
        {
            var reverseExit = targetRoom.Exits.FirstOrDefault(e => e.Direction == reverseDirection && e.TargetRoomId == roomId);
            if (reverseExit != null && reverseExit.IsDoor)
            {
                var reverseKey = (exit.TargetRoomId, reverseDirection);
                if (_doorStates.TryGetValue(reverseKey, out var reverseState))
                {
                    reverseState.IsClosed = false;
                    reverseState.IsLocked = false;
                    reverseState.IsBroken = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Get the opposite direction.
    /// Legacy: rev_dir[] array in constants.c
    /// </summary>
    private static Direction GetReverseDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            _ => direction
        };
    }
}
