using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Score;

[Command("score", Aliases = new[] { "sc" })]
internal sealed class ScoreCommandHandler : ICommandHandler
{
    private readonly EffectiveStats _stats;
    private readonly EliteMud.Application.Commands.Score.ScoreHandler _scoreHandler;

    public ScoreCommandHandler(EffectiveStats stats, RulesContent rules)
    {
        _stats = stats;
        _scoreHandler = new EliteMud.Application.Commands.Score.ScoreHandler(rules);
    }
    
    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _scoreHandler.Handle(context.Player, _stats);
        
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
