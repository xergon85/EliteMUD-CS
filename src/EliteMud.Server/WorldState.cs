using EliteMud.Application;
using EliteMud.Game;
using MobInstance = EliteMud.Application.MobInstance;

namespace EliteMud.Server;

internal sealed class WorldState : IWorldState
{
    private readonly Dictionary<int, MobDefinition> _mobDefinitions;
    private readonly Dictionary<int, List<MobInstance>> _roomMobs;
    private readonly IReadOnlyList<ZoneDefinition> _zones;
    private int _nextMobInstanceId;

    public WorldState(
        WorldDefinition world,
        Dictionary<int, MobDefinition> mobDefinitions,
        Dictionary<int, List<MobInstance>> roomMobs,
        IReadOnlyList<ZoneDefinition> zones)
    {
        World = world;
        _mobDefinitions = mobDefinitions;
        _roomMobs = roomMobs;
        _zones = zones;
    }

    public WorldDefinition World { get; }

    public IReadOnlyDictionary<int, MobDefinition> MobDefinitions => _mobDefinitions;

    public IReadOnlyList<MobInstance> GetMobsInRoom(int roomId)
    {
        return _roomMobs.TryGetValue(roomId, out var mobs)
            ? mobs
            : Array.Empty<MobInstance>();
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
    }

    private void ApplyZoneResets(ZoneDefinition zone)
    {
        foreach (var reset in zone.ResetCommands)
        {
            if (!string.Equals(reset.Type, "LoadMob", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!reset.MobId.HasValue || !reset.RoomId.HasValue)
            {
                continue;
            }

            if (!_mobDefinitions.TryGetValue(reset.MobId.Value, out var mobDefinition))
            {
                continue;
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

            var toSpawn = desiredCount - existing;
            for (var i = 0; i < toSpawn; i++)
            {
                list.Add(new MobInstance(_nextMobInstanceId++, mobDefinition));
            }
        }
    }
}
