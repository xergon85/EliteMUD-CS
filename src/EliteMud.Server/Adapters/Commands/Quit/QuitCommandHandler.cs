using EliteMud.Application.Commands.Shared;
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
        // Legacy do_quit (act.other.c): no quitting in the middle of a fight. Legacy checks the fighting position,
        // which lets a player knocked to the ground mid-fight quit and escape; here any fight counts
        if (context.Player.Fighting != null)
        {
            await context.Session.SendLineAsync("No way!  You're fighting for your life!", cancellationToken);
            return CommandOutcome.Continue;
        }

        await context.Session.SendLineAsync("Goodbye!", cancellationToken);
        return CommandOutcome.Disconnect;
    }
}
