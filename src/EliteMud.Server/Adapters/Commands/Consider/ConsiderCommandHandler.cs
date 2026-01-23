using EliteMud.Application.Commands.Consider;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Consider;

[Command("consider", Aliases = new[] { "con" })]
internal sealed class ConsiderCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ConsiderHandler _considerHandler;

    public ConsiderCommandHandler(IWorldState worldState)
    {
        _worldState = worldState;
        _considerHandler = new ConsiderHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _considerHandler.Handle(context.Player, command.Argument ?? "");
        
        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send multi-line consideration message
        // Legacy: do_consider() in act.informative.c sends three lines
        var lines = result.Message.Split('\n');
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                await context.Session.SendLineAsync(line, cancellationToken);
            }
        }
        
        return CommandOutcome.Continue;
    }
}
