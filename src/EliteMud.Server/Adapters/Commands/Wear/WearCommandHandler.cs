using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wear;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wear;

[Command("wear")]
internal sealed class WearCommandHandler : ICommandHandler
{
    private readonly WearHandler _wearHandler;
    private readonly ActMessenger _act;

    public WearCommandHandler(
        IWorldState worldState,
        ActMessenger act)
    {
        _wearHandler = new WearHandler(worldState);
        _act = act;
    }
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
                context.SendEquipMessage(_act, "You wear $p.", "$n wears $p.", obj);
            }
            return CommandOutcome.Continue;
        }
        
        // If there's a single object, use ActMessage
        if (result.Object is not null)
        {
            context.SendEquipMessage(_act, "You wear $p.", "$n wears $p.", result.Object);
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
