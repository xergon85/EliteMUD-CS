using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Score;

internal sealed class ScoreCommandHandler : ICommandHandler
{
    private readonly EliteMud.Application.Commands.Score.ScoreHandler _scoreHandler;

    public ScoreCommandHandler(EliteMud.Application.Commands.Score.ScoreHandler scoreHandler)
    {
        _scoreHandler = scoreHandler;
    }

    public CommandKind Kind => CommandKind.Score;

    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _scoreHandler.Handle(context.Player);
        
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
