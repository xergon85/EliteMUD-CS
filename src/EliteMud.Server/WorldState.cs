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

        list.Add(new ObjectInstance(_nextObjectInstanceId++, objectDefinition));
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

        // TODO: Implement mob equipment storage
        // For now, this is a placeholder - need to add equipment to MobInstance
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
