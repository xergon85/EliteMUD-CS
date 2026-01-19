using EliteMud.Application.Commands.Examine;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Examine;

internal sealed class ExamineCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ExamineHandler _examineHandler;

    public ExamineCommandHandler(IWorldState worldState)
    {
        _worldState = worldState;
        _examineHandler = new ExamineHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Examine;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _examineHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
