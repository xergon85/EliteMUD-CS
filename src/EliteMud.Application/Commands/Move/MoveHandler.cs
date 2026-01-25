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
        if (player.FightingConnectionId != null)
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

        // Check if there is a closed door blocking the exit
        var room = _worldState.World.GetRoom(player.RoomId);
        var exit = room.Exits.FirstOrDefault(e => e.Direction == direction);
        
        if (exit?.IsDoor == true)
        {
            var doorState = _worldState.GetDoorState(player.RoomId, direction);
            if (doorState?.IsClosed == true)
            {
                return MoveResult.Failed("The door is closed.");
            }
        }

        player.RoomId = targetRoomId;
        return MoveResult.Success();
    }
}
