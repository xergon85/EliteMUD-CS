using EliteMud.Application;

namespace EliteMud.Server;

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
