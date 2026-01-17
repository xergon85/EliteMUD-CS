using EliteMud.Application;

namespace EliteMud.Server;

internal sealed class CommandRouter
{
    private readonly Dictionary<CommandKind, ICommandHandler> _handlers;

    public CommandRouter(IEnumerable<ICommandHandler> handlers)
    {
        _handlers = new Dictionary<CommandKind, ICommandHandler>();
        foreach (var handler in handlers)
        {
            _handlers[handler.Kind] = handler;
        }
    }

    public ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (_handlers.TryGetValue(command.Kind, out var handler))
        {
            return handler.HandleAsync(command, context, cancellationToken);
        }

        return ValueTask.FromResult(CommandOutcome.Unknown);
    }
}
