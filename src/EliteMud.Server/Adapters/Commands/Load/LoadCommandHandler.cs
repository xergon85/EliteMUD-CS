using EliteMud.Application.Commands.Load;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Load;

[Command("load")]
internal sealed class LoadCommandHandler : ICommandHandler
{
    private readonly LoadHandler _loadHandler;

    public LoadCommandHandler(IWorldState worldState)
    {
        _loadHandler = new LoadHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _loadHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
