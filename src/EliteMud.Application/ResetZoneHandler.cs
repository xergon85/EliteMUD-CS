using EliteMud.Game;

namespace EliteMud.Application;

public sealed class ResetZoneHandler
{
    private readonly IWorldState _worldState;

    public ResetZoneHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public ResetZoneResult Handle(PlayerState player, int? zoneId)
    {
        if (zoneId.HasValue)
        {
            if (!_worldState.ResetZone(zoneId.Value))
            {
                return ResetZoneResult.Failed("Zone not found.");
            }

            return ResetZoneResult.Succeeded($"Zone {zoneId.Value} reset.");
        }

        if (!_worldState.ResetZoneForRoom(player.RoomId, out var currentZoneId))
        {
            return ResetZoneResult.Failed("You are not in a zone with resets.");
        }

        return ResetZoneResult.Succeeded($"Zone {currentZoneId} reset.");
    }
}
