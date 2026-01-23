using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.NoOp;

[Command("")]
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
