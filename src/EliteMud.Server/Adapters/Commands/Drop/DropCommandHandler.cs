using EliteMud.Application.Commands.Drop;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Drop;

[Command("drop")]
internal sealed class DropCommandHandler : ICommandHandler
{
    private readonly DropHandler _dropHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public DropCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _dropHandler = new DropHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _dropHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Handle multiple objects (drop all, drop all.item)
        if (result.Objects != null && result.Objects.Count > 0)
        {
            foreach (var obj in result.Objects)
            {
                await context.ActToCharAsync(
                    _actService,
                    "you drop $p.",
                    obj: obj,
                    cancellationToken: cancellationToken);

                await context.ActToNotCharAsync(
                    _actService,
                    _connectionRegistry,
                    "$n drops $p.",
                    obj: obj,
                    cancellationToken: cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        // Handle single object
        await context.ActToCharAsync(
            _actService,
            "you drop $p.",
            obj: result.Object,
            cancellationToken: cancellationToken);

        await context.ActToNotCharAsync(
            _actService,
            _connectionRegistry,
            "$n drops $p.",
            obj: result.Object,
            cancellationToken: cancellationToken);

        return CommandOutcome.Continue;
    }
}
