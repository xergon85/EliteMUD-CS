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
/// </summary>
internal sealed class SkillCommandHandler : ICommandHandler
{
    private readonly ISkillExecutor _executor;
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;
    
    public CommandKind Kind => _executor.CommandKind;
    
    public SkillCommandHandler(
        ISkillExecutor executor,
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _executor = executor;
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
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
        await SendMessagesAsync(result.Messages, context, cancellationToken);
        
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
                new SkillContext(context.Player, context.Id, null, null, argument),
            
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
            if (player.FightingConnectionId == null)
            {
                await context.Session.SendLineAsync("You aren't fighting anyone!", cancellationToken);
                return null;
            }
            
            // Find current fighting target
            if (player.FightingConnectionId.Value > 0)
            {
                // Fighting a player
                var currentTarget = _connectionRegistry.GetConnections()
                    .FirstOrDefault(c => c.Id == player.FightingConnectionId.Value);
                    
                if (currentTarget != null)
                {
                    return new SkillContext(player, context.Id, currentTarget.Player, currentTarget.Id, null);
                }
                
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                return null;
            }
            else
            {
                // Fighting a mob
                var mobInstanceId = -player.FightingConnectionId.Value;
                var currentMob = _worldState.GetMobsInRoom(player.RoomId)
                    .FirstOrDefault(m => m.InstanceId == mobInstanceId);
                
                if (currentMob == null)
                {
                    await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                    return null;
                }
                
                return new SkillContext(player, context.Id, currentMob, null, null);
            }
        }
        
        // Case 2: Target specified
        
        // If already fighting, check if targeting current opponent
        if (player.FightingConnectionId != null)
        {
            if (player.FightingConnectionId.Value > 0)
            {
                var currentTarget = _connectionRegistry.GetConnections()
                    .FirstOrDefault(c => c.Id == player.FightingConnectionId.Value);
                    
                if (currentTarget != null && currentTarget.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return new SkillContext(player, context.Id, currentTarget.Player, currentTarget.Id, targetName);
                }
            }
            else
            {
                var mobInstanceId = -player.FightingConnectionId.Value;
                var currentMob = _worldState.GetMobsInRoom(player.RoomId)
                    .FirstOrDefault(m => m.InstanceId == mobInstanceId 
                        && m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));
                
                if (currentMob != null)
                {
                    return new SkillContext(player, context.Id, currentMob, null, targetName);
                }
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
            return new SkillContext(player, context.Id, targetPlayer.Player, targetPlayer.Id, targetName);
        }
        
        var targetMob = _worldState.GetMobsInRoom(player.RoomId)
            .FirstOrDefault(m => m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        
        if (targetMob != null)
        {
            return new SkillContext(player, context.Id, targetMob, null, targetName);
        }
        
        await context.Session.SendLineAsync($"You don't see '{targetName}' here.", cancellationToken);
        return null;
    }
    
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
            return new SkillContext(player, context.Id, targetPlayer.Player, targetPlayer.Id, targetName);
        }
        
        // Find mob target
        var targetMob = _worldState.GetMobsInRoom(player.RoomId)
            .FirstOrDefault(m => m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        
        if (targetMob != null)
        {
            return new SkillContext(player, context.Id, targetMob, null, targetName);
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
            context.Session.SendLineAsync("That's not a valid direction.", cancellationToken).AsTask().Wait(cancellationToken);
            return null;
        }
        
        return new SkillContext(player, context.Id, null, null, direction);
    }
    
    /// <summary>
    /// Resolve self-targeting (skill targets the user).
    /// </summary>
    private SkillContext ResolveSelf(ConnectionContext context)
    {
        var player = context.Player;
        return new SkillContext(player, context.Id, player, context.Id, null);
    }
    
    /// <summary>
    /// Send skill result messages to appropriate players.
    /// </summary>
    private async Task SendMessagesAsync(
        SkillMessage[] messages,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await SendMessageAsync(message, context, cancellationToken);
        }
    }
    
    /// <summary>
    /// Send a single skill message to appropriate target(s).
    /// </summary>
    private async Task SendMessageAsync(
        SkillMessage message,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var actor = context.Player;
        
        switch (message.Target)
        {
            case SkillMessageTarget.Actor:
                await SendToActorAsync(message.Template, actor, message.Victim, context, cancellationToken);
                break;
            
            case SkillMessageTarget.Victim:
                await SendToVictimAsync(message.Template, actor, message.Victim, cancellationToken);
                break;
            
            case SkillMessageTarget.Room:
                await SendToRoomAsync(message.Template, actor, message.Victim, cancellationToken);
                break;
            
            case SkillMessageTarget.Others:
                await SendToOthersAsync(message.Template, actor, message.Victim, context, cancellationToken);
                break;
        }
    }
    
    private async Task SendToActorAsync(
        string template,
        PlayerState actor,
        object? victim,
        ConnectionContext actorConnection,
        CancellationToken cancellationToken)
    {
        var formatted = _actService.FormatMessage(template, actor, actor, victim);
        await actorConnection.Session.SendLineAsync(formatted, cancellationToken);
    }
    
    private async Task SendToVictimAsync(
        string template,
        PlayerState actor,
        object? victim,
        CancellationToken cancellationToken)
    {
        if (victim is PlayerState victimPlayer)
        {
            var victimConnection = _connectionRegistry.GetConnections()
                .FirstOrDefault(c => c.Player.Id == victimPlayer.Id);
            
            if (victimConnection != null)
            {
                var formatted = _actService.FormatMessage(template, victimPlayer, actor, victimPlayer);
                await victimConnection.Session.SendLineAsync(formatted, cancellationToken);
            }
        }
    }
    
    private async Task SendToRoomAsync(
        string template,
        PlayerState actor,
        object? victim,
        CancellationToken cancellationToken)
    {
        var roomPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == actor.RoomId);
        
        foreach (var observer in roomPlayers)
        {
            var formatted = _actService.FormatMessage(template, observer.Player, actor, victim);
            await observer.Session.SendLineAsync(formatted, cancellationToken);
        }
    }
    
    private async Task SendToOthersAsync(
        string template,
        PlayerState actor,
        object? victim,
        ConnectionContext actorConnection,
        CancellationToken cancellationToken)
    {
        var victimConnectionId = victim is PlayerState victimPlayer
            ? _connectionRegistry.GetConnections().FirstOrDefault(c => c.Player.Id == victimPlayer.Id)?.Id
            : null;
        
        var observers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == actor.RoomId 
                     && c.Id != actorConnection.Id 
                     && (victimConnectionId == null || c.Id != victimConnectionId));
        
        foreach (var observer in observers)
        {
            var formatted = _actService.FormatMessage(template, observer.Player, actor, victim);
            await observer.Session.SendLineAsync(formatted, cancellationToken);
        }
    }
}
