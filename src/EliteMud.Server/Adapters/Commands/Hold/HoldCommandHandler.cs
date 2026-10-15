using EliteMud.Application.Commands.Hold;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Hold;

[Command("hold")]
internal sealed class HoldCommandHandler : ICommandHandler
{
    private readonly HoldHandler _holdHandler;
    private readonly ActMessenger _act;

    public HoldCommandHandler(
        IWorldState worldState,
        ActMessenger act)
    {
        _holdHandler = new HoldHandler(worldState);
        _act = act;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _holdHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        // Success - item equipped
        if (result.Object is not null)
        {
            context.SendEquipMessage(_act, "You grab $p.", "$n grabs $p.", result.Object);
            return CommandOutcome.Continue;
        }
        
        // Slot already occupied - show "You're already holding $p." or "You're already using $p as light source."
        if (result.AlreadyEquipped is not null)
        {
            // Determine message based on slot type
            // Legacy act.obj2.c:647-665 - already_wearing[] array
            var message = result.AlreadyEquipped.Type == "Light"
                ? "You're already using $p as light source."
                : "You're already holding $p.";
                
            _act.ToChar(context.Player, message, obj: result.AlreadyEquipped);
            return CommandOutcome.Continue;
        }
        
        // Other error messages (plain text)
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
        }
        
        return CommandOutcome.Continue;
    }
}
