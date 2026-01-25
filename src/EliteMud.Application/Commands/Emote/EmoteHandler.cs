using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Emote;

public sealed class EmoteHandler
{
    public EmoteResult Handle(PlayerState player, string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return EmoteResult.Failed("Yes.. But what?");
        }

        var trimmedAction = action.Trim();
        var message = $"{player.Name} {trimmedAction} #N";

        return EmoteResult.Succeeded(message, message);
    }
}
