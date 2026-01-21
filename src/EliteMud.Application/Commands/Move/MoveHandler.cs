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

        if (!_worldState.World.TryMove(player.RoomId, direction, out var targetRoomId))
        {
            return MoveResult.Failed("You cannot go that way.");
        }

        player.RoomId = targetRoomId;
        return MoveResult.Success();
    }
}
