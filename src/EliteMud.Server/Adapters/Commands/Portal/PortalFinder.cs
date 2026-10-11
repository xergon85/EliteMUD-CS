using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Portal;

/// <summary>
/// Finds portal objects in the player's room by keyword.
/// </summary>
internal static class PortalFinder
{
    // Legacy: LEVEL_DEITY and LEVEL_IMMORT in structs.h
    public const int DeityLevel = 110;
    public const int ImmortalLevel = 112;

    public static ObjectInstance? FindPortal(IWorldState worldState, PlayerState player, string argument)
    {
        var (index, name) = TargetParser.ParseTarget(argument);
        if (index == 0)
        {
            return null;
        }

        var portals = worldState.GetObjectsInRoom(player.RoomId)
            .Where(obj => obj.Portal is not null)
            .ToList();
        return TargetParser.FindNthMatch(portals, name, index);
    }

    public static bool AnyObjectNamed(IWorldState worldState, PlayerState player, string argument)
    {
        var (index, name) = TargetParser.ParseTarget(argument);
        return index != 0
            && TargetParser.FindNthMatch(worldState.GetObjectsInRoom(player.RoomId), name, index) is not null;
    }
}
