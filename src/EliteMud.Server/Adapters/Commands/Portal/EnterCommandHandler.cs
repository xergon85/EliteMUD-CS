using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Portal;

/// <summary>
/// Enter a portal in the room.
/// Legacy: do_enter() and enter_portal() in act.movement.c
/// </summary>
[Command("enter")]
internal sealed class EnterCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly LookCommandHandler _lookHandler;
    private readonly ConnectionRegistry _connectionRegistry;

    public EnterCommandHandler(
        IWorldState worldState,
        LookCommandHandler lookHandler,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _lookHandler = lookHandler;
        _connectionRegistry = connectionRegistry;
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
            var room = _worldState.World.GetRoom(player.RoomId);
            var message = room.Flags.HasFlag(RoomFlags.Indoors)
                ? "You are already indoors."
                : "You can't seem to find anything to enter.";
            await context.Session.SendLineAsync(message, cancellationToken);
            return CommandOutcome.Continue;
        }

        var portalObject = PortalFinder.FindPortal(_worldState, player, argument);
        if (portalObject?.Portal is not { } portal)
        {
            await context.Session.SendLineAsync(
                $"There is no {argument} here that you can enter.",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        var portalName = portalObject.Definition.ShortDescription;
        var denial = GetDenialMessage(player, portal, portalName);
        if (denial is not null)
        {
            await context.Session.SendLineAsync(denial, cancellationToken);
            return CommandOutcome.Continue;
        }

        await context.Session.SendLineAsync($"You disappear into {portalName}.", cancellationToken);
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} disappears into {portalName}.",
            cancellationToken);

        _worldState.Population.Move(player, portal.Destination);

        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} appears from {portalName}.",
            cancellationToken);

        // Legacy: calls do_look after arriving
        await _lookHandler.HandleAsync(new CommandRequest("look", null, null), context, cancellationToken);

        return CommandOutcome.Continue;
    }

    private string? GetDenialMessage(PlayerState player, PortalState portal, string portalName)
    {
        if (portal.IsClosed)
        {
            return $"The {portalName} appears to be closed.";
        }

        if (portal.IsLocked)
        {
            return $"The {portalName} appears to be locked.";
        }

        // Legacy: level limits apply up to and including LEVEL_IMMORT
        if (player.Level <= PortalFinder.ImmortalLevel && portal.IsTooLowLevel(player.Level))
        {
            return $"You are too weak to enter the {portalName}.";
        }

        if (player.Level <= PortalFinder.ImmortalLevel && portal.IsTooHighLevel(player.Level))
        {
            return $"You are too powerful to enter the {portalName}.";
        }

        if (!_worldState.World.Rooms.TryGetValue(portal.Destination, out var destination))
        {
            return "It appears to be broken...";
        }

        if ((destination.Flags.HasFlag(RoomFlags.GodRoom) && player.Level < PortalFinder.DeityLevel)
            || destination.Flags.HasFlag(RoomFlags.Private))
        {
            return $"The {portalName} repels your entry.";
        }

        return null;
    }
}
