using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wield;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wield;

internal sealed class WieldCommandHandler : ICommandHandler
{
    private readonly WieldHandler _wieldHandler;

    public WieldCommandHandler(IWorldState worldState)
    {
        _wieldHandler = new WieldHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Wield;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _wieldHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
