using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wimpy;

[Command("wimpy")]
internal sealed class WimpyCommandHandler : ICommandHandler
{

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // No argument: show current wimpy setting (legacy: act.other.c:881-886)
        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            if (player.WimpyLevel > 0)
            {
                await context.Session.SendLineAsync(
                    $"Your current wimp level is {player.WimpyLevel} hit points.",
                    cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync(
                    "At the moment, you're not a wimp.  (sure, sure...)",
                    cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        // Parse the wimpy level argument
        if (!short.TryParse(command.Argument, out var wimpyLevel))
        {
            await context.Session.SendLineAsync(
                "Please specify a number for your wimpy level.",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Wimpy 0: disable wimpy (legacy: act.other.c:889-893)
        if (wimpyLevel == 0)
        {
            player.WimpyLevel = 0;
            await context.Session.SendLineAsync(
                "OK, you are now as tough (and stupid) as a knight.",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Negative wimpy: snark response (legacy: act.other.c:895-899)
        if (wimpyLevel < 0)
        {
            await context.Session.SendLineAsync(
                "Heh, heh, heh.. we are jolly funny today, eh?",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Max wimpy is 25% of max HP (legacy: act.other.c:901-905)
        var maxWimpy = player.MaxHitPoints / 4;
        if (wimpyLevel > maxWimpy)
        {
            await context.Session.SendLineAsync(
                "I know that you are a constant wimp, but set it a little lower will you?",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Set the wimpy level (legacy: act.other.c:907-910)
        player.WimpyLevel = wimpyLevel;
        await context.Session.SendLineAsync(
            $"OK, you'll chicken out if you drop below {wimpyLevel} hit points.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}
