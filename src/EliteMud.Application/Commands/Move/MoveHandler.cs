using System.Linq;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Move;

public sealed class MoveHandler
{
    private readonly IWorldState _worldState;

    public MoveHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public MoveResult Handle(PlayerState player, Direction direction)
    {
        // Block movement if player is in combat (legacy: act.movement.c)
        if (player.Fighting != null)
        {
            return MoveResult.Failed("You are fighting!");
        }

        // Block movement if player is not standing (sleeping, resting, sitting)
        // Legacy: act.movement.c checks GET_POS(ch) < POS_STANDING
        if (player.Position < Position.Standing)
        {
            string positionName = player.Position switch
            {
                Position.Sleeping => "sleeping",
                Position.Resting => "resting",
                Position.Sitting => "sitting",
                _ => "in your current state"
            };
            return MoveResult.Failed($"You can't move while {positionName}!");
        }

        if (!_worldState.World.TryMove(player.RoomId, direction, out var targetRoomId))
        {
            return MoveResult.Failed("You cannot go that way.");
        }

        // Validate that the target room exists (some exits may point to -1 or invalid rooms)
        if (!_worldState.World.Rooms.ContainsKey(targetRoomId))
        {
            return MoveResult.Failed("You cannot go that way.");
        }

        // Closed exits block movement, doors or not. Legacy: do_simple_move() in act.movement.c
        var room = _worldState.World.GetRoom(player.RoomId);
        var exit = room.Exits.FirstOrDefault(e => e.Direction == direction);

        if (exit is not null && _worldState.IsExitClosed(player.RoomId, direction))
        {
            return MoveResult.Failed(ClosedExitMessage(exit));
        }

        _worldState.Population.Move(player, targetRoomId);
        return MoveResult.Success();
    }

    // Legacy messages: a secret exit doesn't reveal itself; otherwise name it by its first keyword
    private static string ClosedExitMessage(ExitDefinition exit)
    {
        if (exit.ExitFlags?.Contains("Secret", StringComparer.OrdinalIgnoreCase) == true)
        {
            return "Alas, you cannot go that way...";
        }

        var keyword = exit.Keywords?.FirstOrDefault();
        return keyword is null ? "It seems to be closed." : $"The {keyword} seems to be closed.";
    }
}
