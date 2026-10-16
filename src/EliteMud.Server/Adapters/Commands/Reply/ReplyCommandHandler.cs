using EliteMud.Application.Commands.Reply;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Reply;

[Command("reply")]
internal sealed class ReplyCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ICharacterOutput _output;
    private readonly ReplyHandler _replyHandler;

    public ReplyCommandHandler(IWorldState worldState, ICharacterOutput output)
    {
        _worldState = worldState;
        _output = output;
        _replyHandler = new ReplyHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _replyHandler.Handle(context.Player, _worldState.Population, command.Argument);

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success)
        {
            return CommandOutcome.Continue;
        }

        // Send message to recipient
        if (result.Recipient != null)
        {
            _output.SendLine(result.Recipient, result.RecipientMessage!);

            // Track last tell sender for subsequent replies
            result.Recipient.LastTellSender = context.Player;
        }

        return CommandOutcome.Continue;
    }
}
