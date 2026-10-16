using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Wield;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wield;

[Command("wield")]
internal sealed class WieldCommandHandler : ICommandHandler
{
    private readonly WieldHandler _wieldHandler;
    private readonly ActMessenger _act;

    public WieldCommandHandler(
        IWorldState worldState,
        ActMessenger act)
    {
        _wieldHandler = new WieldHandler(worldState);
        _act = act;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _wieldHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        // Success - item equipped
        if (result.Object is not null)
        {
            context.SendEquipMessage(_act, "You wield $p.", "$n wields $p.", result.Object);
            return CommandOutcome.Continue;
        }
        
        // Slot already occupied - show "You're already wielding $p."
        if (result.AlreadyEquipped is not null)
        {
            _act.ToChar(context.Player, "You're already wielding $p.", obj: result.AlreadyEquipped);
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
