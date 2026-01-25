using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Shout;

public sealed class ShoutHandler
{
    private const short ShoutMovementCost = 10;  // Legacy: holler_move_cost

    public ShoutResult Handle(PlayerState player, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return ShoutResult.ShowHistory();
        }

        // Check if player has enough movement points
        if (player.Movement < ShoutMovementCost)
        {
            return ShoutResult.Failed("You're too exhausted to yell.");
        }

        // Deduct movement cost
        player.Movement -= ShoutMovementCost;

        var trimmedMessage = message.Trim();
        var senderMessage = $"#cYou yell, '{trimmedMessage}#c'#N";
        var broadcastMessage = $"#c{player.Name} yells, '{trimmedMessage}#c'#N";

        return ShoutResult.Succeeded(senderMessage, broadcastMessage);
    }
}
