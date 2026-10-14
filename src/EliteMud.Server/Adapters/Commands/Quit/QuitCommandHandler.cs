using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Quit;

[Command("quit")]
internal sealed class QuitCommandHandler : ICommandHandler
{

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Legacy do_quit (act.other.c): no quitting in the middle of a fight
        if (context.Player.Position == Position.Fighting)
        {
            await context.Session.SendLineAsync("No way!  You're fighting for your life!", cancellationToken);
            return CommandOutcome.Continue;
        }

        await context.Session.SendLineAsync("Goodbye!", cancellationToken);
        return CommandOutcome.Disconnect;
    }
}
