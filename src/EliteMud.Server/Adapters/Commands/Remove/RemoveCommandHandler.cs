using EliteMud.Application.Commands.Remove;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Remove;

[Command("remove", Aliases = new[] { "remove", "rem" })]
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

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _removeHandler.Handle(context.Player, command.Argument ?? string.Empty);

        // Handle multiple objects (remove all, remove all.item)
        if (result.Objects != null && result.Objects.Count > 0)
        {
            foreach (var obj in result.Objects)
            {
                await context.SendEquipMessageAsync(
                    _actService,
                    _connectionRegistry,
                    "You stop using $p.",
                    "$n stops using $p.",
                    obj,
                    cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        // Handle single object
        if (result.Object is not null)
        {
            await context.SendEquipMessageAsync(
                _actService,
                _connectionRegistry,
                "You stop using $p.",
                "$n stops using $p.",
                result.Object,
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Handle error message
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}
