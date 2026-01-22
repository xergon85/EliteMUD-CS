using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wield;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wield;

internal sealed class WieldCommandHandler : ICommandHandler
{
    private readonly WieldHandler _wieldHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public WieldCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _wieldHandler = new WieldHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Wield;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _wieldHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (result.Object is not null)
        {
            await context.ActToCharAsync(
                _actService,
                "You wield $p.",
                obj: result.Object,
                cancellationToken: cancellationToken);

            await context.ActToNotCharAsync(
                _actService,
                _connectionRegistry,
                "$n wields $p.",
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
