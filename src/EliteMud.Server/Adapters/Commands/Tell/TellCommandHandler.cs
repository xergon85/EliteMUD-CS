using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Tell;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Tell;

[Command("tell")]
internal sealed class TellCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly TellHandler _tellHandler;

    public TellCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
        _tellHandler = new TellHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Parse "tell <name> <message>" format
        var argument = command.Argument ?? string.Empty;
        var parts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        
        var recipientName = parts.Length > 0 ? parts[0] : null;
        var message = parts.Length > 1 ? parts[1] : null;

        // Find recipient connection by name (case-insensitive)
        ConnectionContext? recipientConnection = null;
        if (!string.IsNullOrWhiteSpace(recipientName))
        {
            recipientConnection = _connectionRegistry.GetConnections()
                .FirstOrDefault(c => c.Player.Name.Equals(recipientName, StringComparison.OrdinalIgnoreCase));
        }

        var result = _tellHandler.Handle(
            context.Player,
            recipientConnection?.Id,
            recipientConnection?.Player.Name ?? recipientName,
            message);

        // Handle history request (legacy feature - not implemented yet)
        if (result.IsHistoryRequest)
        {
            await context.Session.SendLineAsync("Tell history not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success)
        {
            return CommandOutcome.Continue;
        }

        // Send message to recipient
        if (result.RecipientConnectionId.HasValue && recipientConnection != null)
        {
            await recipientConnection.Session.SendLineAsync(result.RecipientMessage!, cancellationToken);
            
            // Track last tell sender for reply command
            recipientConnection.Player.LastTellSender = context.Id;
        }

        return CommandOutcome.Continue;
    }
}
