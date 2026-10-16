using EliteMud.Application.Commands.Emote;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Emote;

[Command("emote", Aliases = new[] { "me" })]
internal sealed class EmoteCommandHandler : ICommandHandler
{
    private readonly ActMessenger _act;
    private readonly EmoteHandler _emoteHandler;

    public EmoteCommandHandler(ActMessenger act)
    {
        _act = act;
        _emoteHandler = new EmoteHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _emoteHandler.Handle(context.Player, command.Argument);

        // Send message to emote executor (same as room broadcast in legacy)
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // The rest of the room sees it too
        _act.BroadcastToRoom(context.Player, result.BroadcastMessage);

        return CommandOutcome.Continue;
    }
}
