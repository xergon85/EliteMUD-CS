using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Affects;

/// <summary>
/// Adapter for the 'affects' command.
/// Shows all active buffs/debuffs on the player with modifiers and durations.
/// </summary>
[Command("affects", Aliases = new[] { "aff" })]
internal sealed class AffectsCommandHandler : ICommandHandler
{
    private readonly EliteMud.Application.Commands.Affects.AffectsHandler _affectsHandler;

    public AffectsCommandHandler()
    {
        _affectsHandler = new EliteMud.Application.Commands.Affects.AffectsHandler();
    }
    
    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _affectsHandler.Handle(context.Player);
        
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
