using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Who;

[Command("who")]
internal sealed class WhoCommandHandler : ICommandHandler
{
    private readonly WhoHandler _whoHandler;

    public WhoCommandHandler(IWorldState worldState, RulesContent rules)
    {
        _whoHandler = new WhoHandler(worldState, rules);
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _whoHandler.Handle();
        await context.Session.SendAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
