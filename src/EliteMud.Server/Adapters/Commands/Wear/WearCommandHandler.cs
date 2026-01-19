using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wear;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wear;

internal sealed class WearCommandHandler : ICommandHandler
{
    private readonly WearHandler _wearHandler;

    public WearCommandHandler(IWorldState worldState)
    {
        _wearHandler = new WearHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Wear;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _wearHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
