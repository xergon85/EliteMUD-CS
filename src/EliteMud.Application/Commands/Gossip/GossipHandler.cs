using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Gossip;

public sealed class GossipHandler
{
    public GossipResult Handle(PlayerState player, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return GossipResult.ShowHistory();
        }

        var trimmedMessage = message.Trim();
        var senderMessage = $"#YYou gossip, '{trimmedMessage}#Y'#N";
        var broadcastMessage = $"#Y{player.Name} gossips, '{trimmedMessage}#Y'#N";

        return GossipResult.Succeeded(senderMessage, broadcastMessage);
    }
}
