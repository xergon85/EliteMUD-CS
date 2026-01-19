using EliteMud.Application.Commands.Get;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Get;

internal sealed class GetCommandHandler : ICommandHandler
{
    private readonly GetHandler _getHandler;

    public GetCommandHandler(IWorldState worldState)
    {
        _getHandler = new GetHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Get;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _getHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
