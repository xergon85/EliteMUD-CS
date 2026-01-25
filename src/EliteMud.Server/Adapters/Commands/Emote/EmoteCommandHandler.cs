using EliteMud.Application.Commands.Emote;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Emote;

[Command("emote", Aliases = new[] { "me" })]
internal sealed class EmoteCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly EmoteHandler _emoteHandler;

    public EmoteCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
        _emoteHandler = new EmoteHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _emoteHandler.Handle(context.Player, command.Argument);

        // Send message to emote executor (same as room broadcast in legacy)
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // Broadcast to everyone in the room (including the emote executor)
        await BroadcastRoomAsync(context, result.BroadcastMessage, cancellationToken);

        return CommandOutcome.Continue;
    }

    private async ValueTask BroadcastRoomAsync(ConnectionContext actor, string message,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connectionRegistry.GetConnections())
        {
            // Skip the actor - they already got the message
            if (connection.Id == actor.Id)
            {
                continue;
            }

            if (connection.Player.RoomId != actor.Player.RoomId)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }
}
