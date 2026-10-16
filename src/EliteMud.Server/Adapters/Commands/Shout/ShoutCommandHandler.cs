using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Shout;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Shout;

[Command("shout", Aliases = new[] { "yell" })]
internal sealed class ShoutCommandHandler : ICommandHandler
{
    private readonly ICharacterOutput _output;
    private readonly IWorldState _worldState;
    private readonly ShoutHandler _shoutHandler;

    public ShoutCommandHandler(ICharacterOutput output, IWorldState worldState)
    {
        _output = output;
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
        BroadcastZone(context.Player, result.BroadcastMessage, senderZoneId);

        return CommandOutcome.Continue;
    }

    private void BroadcastZone(PlayerState sender, string message, int? senderZoneId)
    {
        foreach (var player in _worldState.Population.Players)
        {
            // Skip the sender - they already got the message
            if (player == sender)
            {
                continue;
            }

            // Get recipient's zone ID
            var recipientRoom = _worldState.World.Rooms.GetValueOrDefault(player.RoomId);
            if (recipientRoom == null)
            {
                continue;
            }

            // Only send to players in the same zone
            if (recipientRoom.ZoneId != senderZoneId)
            {
                continue;
            }

            _output.SendLine(player, message);
        }
    }
}
