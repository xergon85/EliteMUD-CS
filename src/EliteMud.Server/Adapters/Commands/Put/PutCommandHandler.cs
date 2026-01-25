using EliteMud.Application.Commands.Put;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Put;

[Command("put")]
internal sealed class PutCommandHandler : ICommandHandler
{
    private readonly PutHandler _putHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public PutCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _putHandler = new PutHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument ?? string.Empty;
        var result = _putHandler.Handle(context.Player, argument);

        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Success - send message to player and room
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        // TODO: Add act() messages to room when act() system is fully implemented
        // Legacy: act("$n puts $p in $P.", FALSE, ch, obj, cont, TO_ROOM);
        
        return CommandOutcome.Continue;
    }
}
