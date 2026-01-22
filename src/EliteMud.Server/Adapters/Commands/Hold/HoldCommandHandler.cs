using EliteMud.Application.Commands.Hold;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Hold;

internal sealed class HoldCommandHandler : ICommandHandler
{
    private readonly HoldHandler _holdHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public HoldCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _holdHandler = new HoldHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Hold;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _holdHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (result.Object is not null)
        {
            await context.ActToCharAsync(
                _actService,
                "You grab $p.",
                obj: result.Object,
                cancellationToken: cancellationToken);

            await context.ActToNotCharAsync(
                _actService,
                _connectionRegistry,
                "$n grabs $p.",
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
