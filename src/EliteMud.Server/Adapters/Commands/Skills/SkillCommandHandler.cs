using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Skills;

/// <summary>
/// Generic command handler that works for ALL skills.
/// 
/// ZERO CUSTOM CODE PER SKILL - This one handler routes to skill executors,
/// handles target resolution, formats messages, and manages I/O.
/// 
/// To add a new skill:
/// 1. Create ISkillExecutor implementation (Application layer)
/// 2. That's it! Auto-registration makes it a command.
/// 
/// Architecture:
/// - Skill Executor (Application): Business logic, returns SkillResult
/// - This Handler (Server): Target resolution, message formatting, I/O
/// - Formulas (Game): Pure calculations
/// 
/// NOTE: This class should NOT have a [Command] attribute because instances are
/// registered per skill executor in AddSkillExecutors(). The [Command] attribute
/// is placed on each ISkillExecutor implementation instead.
/// </summary>
internal sealed class SkillCommandHandler : ICommandHandler
{
    private readonly ISkillExecutor _executor;
    private readonly IWorldState _worldState;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly SkillMessageSender _messageSender;

    public SkillCommandHandler(
        ISkillExecutor executor,
        IWorldState worldState,
        ConnectionRegistry connectionRegistry,
        SkillMessageSender messageSender)
    {
        _executor = executor;
        _worldState = worldState;
        _connectionRegistry = connectionRegistry;
        _messageSender = messageSender;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Resolve target based on executor's TargetingMode
        var skillContext = await ResolveTargetAsync(
            _executor.Targeting,
            command.Argument?.Trim(),
            context,
            cancellationToken);

        if (skillContext == null)
        {
            // Target resolution failed (error message already sent)
            return CommandOutcome.Continue;
        }

        // 2. Execute skill in Application layer
        var result = _executor.Execute(skillContext);

        // 3. Format and send messages
        await _messageSender.SendAsync(result.Messages, context.Player, cancellationToken);

        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Resolve target based on targeting mode.
    /// Returns null if target resolution failed (error message sent to player).
    /// </summary>
    private async Task<SkillContext?> ResolveTargetAsync(
        TargetingMode mode,
        string? argument,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        return mode switch
        {
            TargetingMode.CurrentFightTarget =>
                await ResolveCurrentOrNamedTargetAsync(argument, context, cancellationToken),

            TargetingMode.RequiredInRoom =>
                await ResolveRequiredTargetAsync(argument, context, cancellationToken),

            TargetingMode.Direction =>
                ResolveDirection(argument, context, cancellationToken),

            TargetingMode.Self =>
                ResolveSelf(context),

            TargetingMode.None =>
                new SkillContext(context.Player, null, argument),

            _ => null
        };
    }

    /// <summary>
    /// Resolve target for CurrentFightTarget mode.
    /// If no target specified: use current fighting opponent.
    /// If target specified: find in room (can start combat or kick current target by name).
    /// </summary>
    private async Task<SkillContext?> ResolveCurrentOrNamedTargetAsync(
        string? targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Case 1: No target specified - use current fighting target
        if (string.IsNullOrWhiteSpace(targetName))
        {
            if (player.Fighting == null)
            {
                await context.Session.SendLineAsync("You aren't fighting anyone!", cancellationToken);
                return null;
            }

            if (!IsHere(player.Fighting, player))
            {
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                return null;
            }

            return new SkillContext(player, player.Fighting, null);
        }

        // Case 2: Target specified

        // If already fighting, the name has to be the current opponent
        if (player.Fighting != null)
        {
            if (IsHere(player.Fighting, player)
                && player.Fighting.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                return new SkillContext(player, player.Fighting, targetName);
            }

            // Trying to switch targets mid-combat
            await context.Session.SendLineAsync("You're already fighting someone else!", cancellationToken);
            return null;
        }

        // Not fighting - find target in room
        var targetPlayer = _connectionRegistry.GetConnections()
            .FirstOrDefault(c => c.Player.RoomId == player.RoomId
                                 && c.Id != context.Id
                                 && c.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetPlayer != null)
        {
            return new SkillContext(player, targetPlayer.Player, targetName);
        }

        var targetMob = _worldState.GetMobsInRoom(player.RoomId)
            .FirstOrDefault(m =>
                m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetMob != null)
        {
            return new SkillContext(player, targetMob, targetName);
        }

        await context.Session.SendLineAsync($"You don't see '{targetName}' here.", cancellationToken);
        return null;
    }

    /// <summary>An opponent the player can still reach: in the same room, and for a player, still connected.</summary>
    private bool IsHere(ICombatant opponent, PlayerState player) => opponent switch
    {
        PlayerState other => other.RoomId == player.RoomId
                             && _connectionRegistry.GetConnections().Any(c => c.Player == other),
        MobInstance mob => _worldState.GetMobsInRoom(player.RoomId).Contains(mob),
        _ => false
    };

    /// <summary>
    /// Resolve target for RequiredInRoom mode.
    /// Must specify target name - cannot auto-target fighting opponent.
    /// </summary>
    private async Task<SkillContext?> ResolveRequiredTargetAsync(
        string? targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        if (string.IsNullOrWhiteSpace(targetName))
        {
            await context.Session.SendLineAsync("Who do you want to target?", cancellationToken);
            return null;
        }

        // Find player target
        var targetPlayer = _connectionRegistry.GetConnections()
            .FirstOrDefault(c => c.Player.RoomId == player.RoomId
                                 && c.Id != context.Id
                                 && c.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetPlayer != null)
        {
            return new SkillContext(player, targetPlayer.Player, targetName);
        }

        // Find mob target
        var targetMob = _worldState.GetMobsInRoom(player.RoomId)
            .FirstOrDefault(m =>
                m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetMob != null)
        {
            return new SkillContext(player, targetMob, targetName);
        }

        await context.Session.SendLineAsync($"You don't see '{targetName}' here.", cancellationToken);
        return null;
    }

    /// <summary>
    /// Resolve direction argument (north, south, east, west, up, down).
    /// </summary>
    private SkillContext? ResolveDirection(
        string? direction,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        if (string.IsNullOrWhiteSpace(direction))
        {
            context.Session.SendLineAsync("Which direction?", cancellationToken).AsTask().Wait(cancellationToken);
            return null;
        }

        // Validate direction (basic validation - executor can do more specific checks)
        var validDirections = new[] { "north", "south", "east", "west", "up", "down", "n", "s", "e", "w", "u", "d" };
        if (!validDirections.Contains(direction.ToLowerInvariant()))
        {
            context.Session.SendLineAsync("That's not a valid direction.", cancellationToken).AsTask()
                .Wait(cancellationToken);
            return null;
        }

        return new SkillContext(player, null, direction);
    }

    /// <summary>
    /// Resolve self-targeting (skill targets the user).
    /// </summary>
    private SkillContext ResolveSelf(ConnectionContext context)
    {
        var player = context.Player;
        return new SkillContext(player, player, null);
    }
}
