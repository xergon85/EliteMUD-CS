using EliteMud.Application.Commands.Remove;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Remove;

internal sealed class RemoveCommandHandler : ICommandHandler
{
    private readonly RemoveHandler _removeHandler;

    public RemoveCommandHandler(IWorldState worldState)
    {
        _removeHandler = new RemoveHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Remove;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _removeHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
