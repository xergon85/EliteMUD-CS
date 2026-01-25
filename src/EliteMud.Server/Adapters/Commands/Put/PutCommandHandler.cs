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

        // Handle multiple objects (put all, put all.item)
        if (result.Objects != null && result.Objects.Count > 0 && result.ContainerName != null)
        {
            foreach (var obj in result.Objects)
            {
                await context.Session.SendLineAsync(
                    $"You put {obj.ShortDescription} in {result.ContainerName}.", 
                    cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        // Handle single object - send message
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
