using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Kick;

/// <summary>
/// Thin adapter for kick command - routes to Application layer for execution.
/// 
/// CLEAN ARCHITECTURE PATTERN:
/// - Server layer (this class): Routing, message formatting, I/O
/// - Application layer (KickSkillExecutor): Business logic, skill execution
/// - Game layer (KickSkill): Pure domain logic (formulas, calculations)
/// 
/// This handler should contain MINIMAL logic - just routing and presentation.
/// </summary>
internal sealed class KickCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly KickSkillExecutor _kickExecutor;

    public KickCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        KickSkillExecutor kickExecutor)
    {
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
        _kickExecutor = kickExecutor;
    }

    public CommandKind Kind => CommandKind.Kick;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var targetName = command.Argument?.Trim();
        var player = context.Player;

        // Case 1: No target specified - kick current fighting target
        if (string.IsNullOrWhiteSpace(targetName))
        {
            if (player.FightingConnectionId == null)
            {
                await context.Session.SendLineAsync("You aren't fighting anyone!", cancellationToken);
                return CommandOutcome.Continue;
            }

            // Find current fighting target
            // Check if fighting a player (PvP) - positive connection ID
            if (player.FightingConnectionId.Value > 0)
            {
                var currentTarget = _connectionRegistry.GetConnections()
                    .FirstOrDefault(c => c.Id == player.FightingConnectionId.Value);

                if (currentTarget != null)
                {
                    // Fighting a player - kick them
                    return await ExecuteKick(context, currentTarget.Player, currentTarget, cancellationToken);
                }

                // Connection not found
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                return CommandOutcome.Continue;
            }

            // Fighting a mob (negative connection ID) - find which mob
            var mobInstanceId = -player.FightingConnectionId.Value;
            var mobs = _worldState.GetMobsInRoom(player.RoomId);
            var currentMob = mobs.FirstOrDefault(m => m.InstanceId == mobInstanceId);

            if (currentMob == null)
            {
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                return CommandOutcome.Continue;
            }

            // Fighting a mob - kick them
            return await ExecuteKick(context, currentMob, victimConnection: null, cancellationToken);
        }

        // Case 2: Target specified - can start combat or kick current target by name

        // If already fighting, check if they're trying to kick their current target
        if (player.FightingConnectionId != null)
        {
            // Check if fighting a player (positive ID)
            if (player.FightingConnectionId.Value > 0)
            {
                var currentTarget = _connectionRegistry.GetConnections()
                    .FirstOrDefault(c => c.Id == player.FightingConnectionId.Value);

                if (currentTarget != null &&
                    currentTarget.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    // Kicking current player opponent
                    return await ExecuteKick(context, currentTarget.Player, currentTarget, cancellationToken);
                }
            }
            else
            {
                // Fighting a mob (negative ID)
                var mobInstanceId = -player.FightingConnectionId.Value;
                var currentMob = _worldState.GetMobsInRoom(player.RoomId)
                    .FirstOrDefault(m => m.InstanceId == mobInstanceId
                                         && m.Definition.ShortDescription.Contains(targetName,
                                             StringComparison.OrdinalIgnoreCase));

                if (currentMob != null)
                {
                    // Kicking current mob opponent
                    return await ExecuteKick(context, currentMob, victimConnection: null, cancellationToken);
                }
            }

            // Trying to switch targets mid-combat
            await context.Session.SendLineAsync("You're already fighting someone else!", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Not fighting - try to start combat with kick
        // First check for player target in room
        var targetPlayer = _connectionRegistry.GetConnections()
            .FirstOrDefault(c => c.Player.RoomId == player.RoomId
                                 && c.Id != context.Id
                                 && c.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetPlayer != null)
        {
            return await ExecuteKick(context, targetPlayer.Player, targetPlayer, cancellationToken);
        }

        // Check for mob target in room
        var targetMob = _worldState.GetMobsInRoom(player.RoomId)
            .FirstOrDefault(m =>
                m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetMob != null)
        {
            return await ExecuteKick(context, targetMob, victimConnection: null, cancellationToken);
        }

        await context.Session.SendLineAsync("Kick who?", cancellationToken);
        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Thin adapter: routes kick execution to Application layer and formats messages.
    /// </summary>
    private async ValueTask<CommandOutcome> ExecuteKick(
        ConnectionContext attacker,
        ICombatant victim,
        ConnectionContext? victimConnection,
        CancellationToken cancellationToken)
    {
        var player = attacker.Player;
        var victimConnectionId = victimConnection?.Id;

        // Execute kick in Application layer
        var result = _kickExecutor.Execute(player, victim, attacker.Id, victimConnectionId);

        // Handle cannot use
        if (!result.CanUse)
        {
            await attacker.Session.SendLineAsync(result.CannotUseMessage!, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Handle miss
        if (!result.Hit)
        {
            await SendActMessageAsync(
                "you try to kick $N, but miss!",
                player,
                victim,
                victimConnection,
                ActTarget.ToChar,
                cancellationToken);

            await SendActMessageAsync(
                "$n tries to kick you, but misses!",
                player,
                victim,
                victimConnection,
                ActTarget.ToVict,
                cancellationToken);

            await SendActMessageAsync(
                "$n tries to kick $N, but misses!",
                player,
                victim,
                victimConnection,
                ActTarget.ToNotVict,
                cancellationToken);

            return CommandOutcome.Continue;
        }

        // Handle hit - show dodge message if dodged
        if (result is { VictimDodged: true, DodgeMessage: not null } && victimConnection != null)
        {
            await victimConnection.Session.SendLineAsync(result.DodgeMessage, cancellationToken);
        }

        // Send hit messages with damage
        await SendActMessageAsync(
            $"your kick hits $N [{result.Damage}]",
            player,
            victim,
            victimConnection,
            ActTarget.ToChar,
            cancellationToken);

        await SendActMessageAsync(
            $"$n kicks you! [{result.Damage}]",
            player,
            victim,
            victimConnection,
            ActTarget.ToVict,
            cancellationToken);

        await SendActMessageAsync(
            "$n kicks $N!",
            player,
            victim,
            victimConnection,
            ActTarget.ToNotVict,
            cancellationToken);

        // Handle death
        if (!result.VictimDied) return CommandOutcome.Continue;

        await SendActMessageAsync(
            "$N is DEAD!!",
            player,
            victim,
            victimConnection,
            ActTarget.ToChar,
            cancellationToken);

        await SendActMessageAsync(
            "$N is dead! R.I.P.",
            player,
            victim,
            victimConnection,
            ActTarget.ToNotVict,
            cancellationToken);

        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Send an act() message to appropriate targets based on ActTarget flags.
    /// Handles both player and mob victims.
    /// </summary>
    private async Task SendActMessageAsync(
        string message,
        PlayerState actor,
        object victim,
        ConnectionContext? victimConnection,
        ActTarget target,
        CancellationToken cancellationToken)
    {
        // Send to actor (ToChar)
        if (target.HasFlag(ActTarget.ToChar))
        {
            var actorConnection = _connectionRegistry.GetConnections()
                .FirstOrDefault(c => c.Player.Id == actor.Id);

            if (actorConnection != null)
            {
                var formattedMsg = _actService.FormatMessage(message, actor, actor, victim);
                await actorConnection.Session.SendLineAsync(formattedMsg, cancellationToken);
            }
        }

        // Send to victim (ToVict) - only if victim is a player
        if (target.HasFlag(ActTarget.ToVict) && victimConnection != null)
        {
            var formattedMsg =
                _actService.FormatMessage(message, victimConnection.Player, actor, victimConnection.Player);
            await victimConnection.Session.SendLineAsync(formattedMsg, cancellationToken);
        }

        // Send to everyone in room (ToRoom)
        if (target.HasFlag(ActTarget.ToRoom))
        {
            var roomPlayers = _connectionRegistry.GetConnections()
                .Where(c => c.Player.RoomId == actor.RoomId);

            foreach (var observer in roomPlayers)
            {
                var formattedMsg = _actService.FormatMessage(message, observer.Player, actor, victim);
                await observer.Session.SendLineAsync(formattedMsg, cancellationToken);
            }
        }

        // Send to everyone in room except actor and victim (ToNotVict)
        if (target.HasFlag(ActTarget.ToNotVict))
        {
            var observers = _connectionRegistry.GetConnections()
                .Where(c => c.Player.RoomId == actor.RoomId
                            && c.Player.Id != actor.Id
                            && (victimConnection == null || c.Id != victimConnection.Id));

            foreach (var observer in observers)
            {
                var formattedMsg = _actService.FormatMessage(message, observer.Player, actor, victim);
                await observer.Session.SendLineAsync(formattedMsg, cancellationToken);
            }
        }
    }
}
