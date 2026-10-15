using EliteMud.Application.Commands.Gossip;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Gossip;

[Command("gossip", Aliases = new[] { "gos" })]
internal sealed class GossipCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ICharacterOutput _output;
    private readonly GossipHandler _gossipHandler;

    public GossipCommandHandler(IWorldState worldState, ICharacterOutput output)
    {
        _worldState = worldState;
        _output = output;
        _gossipHandler = new GossipHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _gossipHandler.Handle(context.Player, command.Argument);

        // Handle history request (legacy feature - not implemented yet)
        if (result.IsHistoryRequest)
        {
            await context.Session.SendLineAsync("Gossip history not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // Broadcast to all connected players (global channel)
        foreach (var player in _worldState.Population.Players.Where(player => player != context.Player))
        {
            _output.SendLine(player, result.BroadcastMessage);
        }

        return CommandOutcome.Continue;
    }
}
