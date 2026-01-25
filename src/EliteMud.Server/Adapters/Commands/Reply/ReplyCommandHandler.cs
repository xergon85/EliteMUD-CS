using EliteMud.Application.Commands.Reply;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Reply;

[Command("reply")]
internal sealed class ReplyCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly ReplyHandler _replyHandler;

    public ReplyCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
        _replyHandler = new ReplyHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var message = command.Argument;

        // Find recipient connection by last tell sender ID
        ConnectionContext? recipientConnection = null;
        if (context.Player.LastTellSender.HasValue)
        {
            recipientConnection = _connectionRegistry.GetConnections()
                .FirstOrDefault(c => c.Id == context.Player.LastTellSender.Value);
        }

        var result = _replyHandler.Handle(
            context.Player,
            context.Player.LastTellSender,
            recipientConnection?.Player.Name,
            message);

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
            
            // Track last tell sender for subsequent replies
            recipientConnection.Player.LastTellSender = context.Id;
        }

        return CommandOutcome.Continue;
    }
}
