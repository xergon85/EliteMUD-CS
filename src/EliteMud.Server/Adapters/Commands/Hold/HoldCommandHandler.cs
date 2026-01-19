using EliteMud.Application.Commands.Hold;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Hold;

internal sealed class HoldCommandHandler : ICommandHandler
{
    private readonly HoldHandler _holdHandler;

    public HoldCommandHandler(IWorldState worldState)
    {
        _holdHandler = new HoldHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Hold;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _holdHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
