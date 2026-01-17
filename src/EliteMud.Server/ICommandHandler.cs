using EliteMud.Application;

namespace EliteMud.Server;

internal interface ICommandHandler
{
    CommandKind Kind { get; }

    ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken);
}
