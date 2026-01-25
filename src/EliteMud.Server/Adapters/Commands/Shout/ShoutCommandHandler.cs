using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Shout;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Shout;

[Command("shout", Aliases = new[] { "yell" })]
internal sealed class ShoutCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly IWorldState _worldState;
    private readonly ShoutHandler _shoutHandler;

    public ShoutCommandHandler(ConnectionRegistry connectionRegistry, IWorldState worldState)
    {
        _connectionRegistry = connectionRegistry;
        _worldState = worldState;
        _shoutHandler = new ShoutHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _shoutHandler.Handle(context.Player, command.Argument);

        // Handle history request (legacy feature - not implemented yet)
        if (result.IsHistoryRequest)
        {
            await context.Session.SendLineAsync("Shout history not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // Get sender's zone ID
        var senderRoom = _worldState.World.Rooms.GetValueOrDefault(context.Player.RoomId);
        if (senderRoom == null)
        {
            return CommandOutcome.Continue;
        }

        var senderZoneId = senderRoom.ZoneId;

        // Broadcast to all players in the same zone
        await BroadcastZoneAsync(context, result.BroadcastMessage, senderZoneId, cancellationToken);

        return CommandOutcome.Continue;
    }

    private async ValueTask BroadcastZoneAsync(ConnectionContext sender, string message, int? senderZoneId,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connectionRegistry.GetConnections())
        {
            // Skip the sender - they already got the message
            if (connection.Id == sender.Id)
            {
                continue;
            }

            // Get recipient's zone ID
            var recipientRoom = _worldState.World.Rooms.GetValueOrDefault(connection.Player.RoomId);
            if (recipientRoom == null)
            {
                continue;
            }

            // Only send to players in the same zone
            if (recipientRoom.ZoneId != senderZoneId)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }
}
