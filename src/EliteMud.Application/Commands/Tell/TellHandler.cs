using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Tell;

public sealed class TellHandler
{
    public TellResult Handle(PlayerState sender, int? recipientConnectionId, string? recipientName, string? message)
    {
        // Check if both recipient and message are provided
        if (string.IsNullOrWhiteSpace(recipientName) || string.IsNullOrWhiteSpace(message))
        {
            return TellResult.ShowHistory();
        }

        // Check if recipient was found
        if (!recipientConnectionId.HasValue)
        {
            return TellResult.Failed("No such player around.");
        }

        var trimmedMessage = message.Trim();
        var senderMessage = $"#bYou tell {recipientName} '{trimmedMessage}#b'#N";
        var recipientMessage = $"#b{sender.Name} tells you '{trimmedMessage}#b'#N";

        return TellResult.Succeeded(senderMessage, recipientMessage, recipientConnectionId.Value);
    }
}
