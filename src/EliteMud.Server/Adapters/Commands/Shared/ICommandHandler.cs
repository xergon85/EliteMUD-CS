using EliteMud.Application.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal interface ICommandHandler
{
    CommandKind Kind { get; }

    ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken);
}
