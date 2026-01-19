using EliteMud.Application.Commands.Look;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Examine;

internal sealed class ExamineCommandHandler : ICommandHandler
{
    private readonly LookHandler _lookHandler;

    public ExamineCommandHandler(IWorldState worldState)
    {
        _lookHandler = new LookHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Examine;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Examine is just an alias for "look <target>"
        var result = _lookHandler.HandleLookAt(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}
