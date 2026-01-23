using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.NoOp;

/// <summary>
/// Handles empty commands (when user presses Enter with no input).
/// This is a special case handler that doesn't use the [Command] attribute.
/// </summary>
internal sealed class NoOpCommandHandler : ICommandHandler
{

    public ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(CommandOutcome.Continue);
    }
}
