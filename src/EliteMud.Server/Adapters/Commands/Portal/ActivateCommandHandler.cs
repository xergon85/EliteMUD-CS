using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Portal;

/// <summary>
/// Open a closed portal by carrying its key object (e.g. a ticket).
/// Legacy: do_activate() in act.movement.c
/// </summary>
[Command("activate")]
internal sealed class ActivateCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessenger _act;

    public ActivateCommandHandler(IWorldState worldState, ActMessenger act)
    {
        _worldState = worldState;
        _act = act;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;
        var argument = command.Argument?.Trim() ?? string.Empty;

        if (argument.Length == 0)
        {
            await context.Session.SendLineAsync("Activate what???", cancellationToken);
            return CommandOutcome.Continue;
        }

        var portalObject = PortalFinder.FindPortal(_worldState, player, argument);
        if (portalObject?.Portal is not { } portal)
        {
            var message = PortalFinder.AnyObjectNamed(_worldState, player, argument)
                ? $"You can't seem to activate the {argument}..."
                : $"There is no {argument} here.";
            await context.Session.SendLineAsync(message, cancellationToken);
            return CommandOutcome.Continue;
        }

        var portalName = portalObject.Definition.ShortDescription;
        if (!portal.IsClosed)
        {
            await context.Session.SendLineAsync("It is already active.", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (!CanActivate(player, portal))
        {
            await context.Session.SendLineAsync("You don't seem to have the proper item.", cancellationToken);
            return CommandOutcome.Continue;
        }

        portal.IsClosed = false;
        await context.Session.SendLineAsync($"You activate the {portalName}.", cancellationToken);
        _act.BroadcastToRoom(context.Player, $"{player.Name} activates the {portalName}.");

        return CommandOutcome.Continue;
    }

    // Legacy: an immortal, or anyone carrying the portal's key object, can activate it.
    // Like legacy, the key isn't used up. (Legacy also required immortals to carry some item,
    // a side effect of its inventory loop; that quirk isn't kept.)
    private bool CanActivate(PlayerState player, PortalState portal)
    {
        if (player.Level >= PortalFinder.ImmortalLevel)
        {
            return true;
        }

        return portal.HasKey
            && player.Inventory.Any(item => item.Definition.Id == portal.KeyObjectId);
    }
}
