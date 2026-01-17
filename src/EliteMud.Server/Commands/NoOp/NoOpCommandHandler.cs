using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.NoOp;

internal sealed class NoOpCommandHandler : ICommandHandler
{
    public CommandKind Kind => CommandKind.None;

    public ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(CommandOutcome.Continue);
    }
}
