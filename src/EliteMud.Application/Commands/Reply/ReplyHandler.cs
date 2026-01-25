using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Reply;

public sealed class ReplyHandler
{
    public ReplyResult Handle(PlayerState sender, int? lastTellSenderConnectionId, string? recipientName, string? message)
    {
        // Check if message is provided
        if (string.IsNullOrWhiteSpace(message))
        {
            return ReplyResult.Failed("What is your reply?");
        }

        // Check if there's someone to reply to
        if (!lastTellSenderConnectionId.HasValue)
        {
            return ReplyResult.Failed("You have no-one to reply to!");
        }

        var trimmedMessage = message.Trim();
        var senderMessage = $"#bYou tell {recipientName} '{trimmedMessage}#b'#N";
        var recipientMessage = $"#b{sender.Name} tells you '{trimmedMessage}#b'#N";

        return ReplyResult.Succeeded(senderMessage, recipientMessage, lastTellSenderConnectionId.Value);
    }
}
