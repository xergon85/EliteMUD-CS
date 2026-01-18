using EliteMud.Game;

namespace EliteMud.Application.World;

public sealed record MobInstance(int InstanceId, MobDefinition Definition);

public sealed record ObjectInstance(int InstanceId, ObjectDefinition Definition);

public interface IWorldState
{
    WorldDefinition World { get; }

    IReadOnlyList<MobInstance> GetMobsInRoom(int roomId);

    IReadOnlyList<ObjectInstance> GetObjectsInRoom(int roomId);

    bool ResetZone(int zoneId);

    bool ResetZoneForRoom(int roomId, out int zoneId);
}
