using EliteMud.Game;

namespace EliteMud.Application;

public sealed record MobInstance(int InstanceId, MobDefinition Definition);

public interface IWorldState
{
    WorldDefinition World { get; }

    IReadOnlyList<MobInstance> GetMobsInRoom(int roomId);

    bool ResetZone(int zoneId);

    bool ResetZoneForRoom(int roomId, out int zoneId);
}
