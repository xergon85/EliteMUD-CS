using EliteMud.Game;

namespace EliteMud.Application;

public sealed class SayHandler
{
    public SayResult Handle(PlayerState player, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return SayResult.Failed("Say what?");
        }

        var trimmed = message.Trim();
        return SayResult.Succeeded(
            $"You say, '{trimmed}'.",
            $"{player.Name} says, '{trimmed}'.");
    }
}
