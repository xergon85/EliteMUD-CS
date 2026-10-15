using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Reply;

public sealed class ReplyHandler
{
    /// <summary>Answers the last player who sent a tell, if they are still playing (legacy do_reply).</summary>
    public ReplyResult Handle(PlayerState sender, WorldPopulation population, string? message)
    {
        var recipient = sender.LastTellSender;
        if (recipient == null)
        {
            return ReplyResult.Failed("You have no-one to reply to!");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return ReplyResult.Failed("What is your reply?");
        }

        // Someone who logs in again is someone else: legacy compares the character itself
        if (!population.IsInGame(recipient))
        {
            return ReplyResult.Failed("They are no longer playing.");
        }

        var trimmedMessage = message.Trim();
        var senderMessage = $"#bYou tell {recipient.Name} '{trimmedMessage}#b'#N";
        var recipientMessage = $"#b{sender.Name} tells you '{trimmedMessage}#b'#N";

        return ReplyResult.Succeeded(senderMessage, recipientMessage, recipient);
    }
}
