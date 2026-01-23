using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Stat;

/// <summary>
/// Adapter for the 'stat' command.
/// Shows detailed character statistics including base stats, modifiers, 
/// AC, hitroll, damroll, THAC0, saves, clan info, and innate abilities.
/// </summary>
[Command("stat", Aliases = new[] { "st" })]
internal sealed class StatCommandHandler : ICommandHandler
{
    private readonly EliteMud.Application.Commands.Stat.StatHandler _statHandler;

    public StatCommandHandler(IWorldState worldState)
    {
        _statHandler = new EliteMud.Application.Commands.Stat.StatHandler(worldState);
    }
    
    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _statHandler.Handle(context.Player);
        
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
