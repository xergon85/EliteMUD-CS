using EliteMud.Application.Commands.Gossip;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Gossip;

[Command("gossip", Aliases = new[] { "gos" })]
internal sealed class GossipCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly GossipHandler _gossipHandler;

    public GossipCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
        _gossipHandler = new GossipHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _gossipHandler.Handle(context.Player, command.Argument);

        // Handle history request (legacy feature - not implemented yet)
        if (result.IsHistoryRequest)
        {
            await context.Session.SendLineAsync("Gossip history not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // Broadcast to all connected players (global channel)
        await BroadcastGlobalAsync(context, result.BroadcastMessage, cancellationToken);

        return CommandOutcome.Continue;
    }

    private async ValueTask BroadcastGlobalAsync(ConnectionContext sender, string message,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connectionRegistry.GetConnections())
        {
            // Skip the sender - they already got the message
            if (connection.Id == sender.Id)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }
}
