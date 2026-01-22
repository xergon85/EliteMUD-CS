using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wear;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wear;

internal sealed class WearCommandHandler : ICommandHandler
{
    private readonly WearHandler _wearHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public WearCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _wearHandler = new WearHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Wear;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _wearHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        // If there's a list of objects (from "wear all"), echo each using ActMessage
        if (result.Objects is not null && result.Objects.Count > 0)
        {
            foreach (var obj in result.Objects)
            {
                await context.SendEquipMessageAsync(
                    _actService,
                    _connectionRegistry,
                    "You wear $p.",
                    "$n wears $p.",
                    obj,
                    cancellationToken);
            }
            return CommandOutcome.Continue;
        }
        
        // If there's a single object, use ActMessage
        if (result.Object is not null)
        {
            await context.SendEquipMessageAsync(
                _actService,
                _connectionRegistry,
                "You wear $p.",
                "$n wears $p.",
                result.Object,
                cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Otherwise send the error message
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
        }
        
        return CommandOutcome.Continue;
    }
}
