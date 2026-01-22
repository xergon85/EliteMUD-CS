using EliteMud.Application.Commands.Remove;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Remove;

internal sealed class RemoveCommandHandler : ICommandHandler
{
    private readonly RemoveHandler _removeHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public RemoveCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _removeHandler = new RemoveHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Remove;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _removeHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (result.Object is not null)
        {
            await context.ActToCharAsync(
                _actService,
                "You stop using $p.",
                obj: result.Object,
                cancellationToken: cancellationToken);

            await context.ActToNotCharAsync(
                _actService,
                _connectionRegistry,
                "$n stops using $p.",
                obj: result.Object,
                cancellationToken: cancellationToken);
            return CommandOutcome.Continue;
        }
        
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
        }
        
        return CommandOutcome.Continue;
    }
}
