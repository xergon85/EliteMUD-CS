using EliteMud.Application.Commands.Get;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Get;

internal sealed class GetCommandHandler : ICommandHandler
{
    private readonly GetHandler _getHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public GetCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _getHandler = new GetHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Get;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _getHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Success - use ActMessage to broadcast
        // To actor: "You get $o."
        // To room: "$n gets $o."
        await context.ActToCharAsync(
            _actService,
            "you get $p.",
            obj: result.Object,
            cancellationToken: cancellationToken);

        await context.ActToNotCharAsync(
            _actService,
            _connectionRegistry,
            "$n gets $p.",
            obj: result.Object,
            cancellationToken: cancellationToken);

        return CommandOutcome.Continue;
    }
}
