using EliteMud.Application.Commands.Drop;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Drop;

internal sealed class DropCommandHandler : ICommandHandler
{
    private readonly DropHandler _dropHandler;

    public DropCommandHandler(IWorldState worldState)
    {
        _dropHandler = new DropHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Drop;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _dropHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
