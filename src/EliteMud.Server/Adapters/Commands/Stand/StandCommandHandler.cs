using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Stand;

internal sealed class StandCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public StandCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Stand;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions
        var validationResult = PositionChangeValidator.Validate(player, Position.Standing, "standing");
        if (!validationResult.IsValid)
        {
            await context.Session.SendLineAsync(validationResult.ErrorMessage!, cancellationToken);
            return CommandOutcome.Continue;
        }

        player.Position = Position.Standing;
        await context.Session.SendLineAsync("You stand up.", cancellationToken);
        
        var roomMessage = $"{player.Name} stands up.";
        var playersInRoom = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id);
        
        foreach (var observer in playersInRoom)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}
