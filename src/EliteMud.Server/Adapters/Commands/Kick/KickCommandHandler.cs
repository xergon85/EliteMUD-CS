using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Kick;

internal sealed class KickCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly KickSkill _kickSkill;

    public KickCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
        _kickSkill = new KickSkill(); // TODO: Inject from SkillRegistry when available
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
                
                // Connection not found - stop fighting
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                CombatCalculator.StopFighting(player);
                return CommandOutcome.Continue;
            }

            // Fighting a mob (negative connection ID) - find which mob
            var mobInstanceId = -player.FightingConnectionId.Value;
            var mobs = _worldState.GetMobsInRoom(player.RoomId);
            var currentMob = mobs.FirstOrDefault(m => m.InstanceId == mobInstanceId);
            
            if (currentMob == null)
            {
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                CombatCalculator.StopFighting(player);
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
                    
                if (currentTarget != null && currentTarget.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
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
                        && m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));
                
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
            .FirstOrDefault(m => m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        
        if (targetMob != null)
        {
            return await ExecuteKick(context, targetMob, victimConnection: null, cancellationToken);
        }

        await context.Session.SendLineAsync("Kick who?", cancellationToken);
        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Unified kick execution for any combatant type (player or mob).
    /// </summary>
    private async ValueTask<CommandOutcome> ExecuteKick(
        ConnectionContext attacker,
        ICombatant victim,
        ConnectionContext? victimConnection,
        CancellationToken cancellationToken)
    {
        var player = attacker.Player;

        // Check if player can use kick skill
        if (!_kickSkill.CanUse(player))
        {
            await attacker.Session.SendLineAsync(
                _kickSkill.GetCannotUseMessage(player), cancellationToken);
            return CommandOutcome.Continue;
        }

        // If not already fighting, start combat
        if (player.FightingConnectionId == null)
        {
            if (victimConnection != null)
            {
                // PvP - both sides fight each other
                CombatCalculator.SetFighting(player, victimConnection.Id);
                CombatCalculator.SetFighting(victimConnection.Player, attacker.Id);
            }
            else
            {
                // PvE - player fights mob (mob fights back handled elsewhere)
                var mobInstance = (MobInstance)victim;
                CombatCalculator.SetFighting(player, -mobInstance.InstanceId);
                mobInstance.FightingConnectionId = attacker.Id;
            }
        }

        // Execute kick using unified combat logic
        bool hit = KickSkill.RollHit(player, victim);
        int damage = hit ? KickSkill.CalculateDamage(player) : 0;

        if (!hit)
        {
            // Miss
            await attacker.Session.SendLineAsync(
                $"You try to kick {victim.Name}, but miss!", cancellationToken);
            
            if (victimConnection != null)
            {
                await victimConnection.Session.SendLineAsync(
                    $"{player.Name} tries to kick you, but misses!", cancellationToken);
                await BroadcastToRoomPvP(attacker, victimConnection,
                    $"{player.Name} tries to kick {victim.Name}, but misses!", cancellationToken);
            }
            else
            {
                await BroadcastToRoomPvE(attacker,
                    $"{player.Name} tries to kick {victim.Name}, but misses!", cancellationToken);
            }
        }
        else
        {
            // Hit - apply damage
            if (victimConnection != null)
            {
                // Player victim - use CombatCalculator for dodge support
                var damageResult = CombatCalculator.ApplyDamage(victimConnection.Player, damage);
                
                // Build messages
                string attackerMsg = $"Your kick hits {victim.Name} [{damageResult.Damage}]";
                string victimMsg = $"{player.Name} kicks you! [{damageResult.Damage}]";
                
                // Add dodge notification if dodged
                if (damageResult.Dodged && !string.IsNullOrEmpty(damageResult.Message))
                {
                    victimMsg = damageResult.Message + " " + victimMsg;
                }

                await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
                await victimConnection.Session.SendLineAsync(victimMsg, cancellationToken);
                await BroadcastToRoomPvP(attacker, victimConnection,
                    $"{player.Name} kicks {victim.Name}!", cancellationToken);
                
                // Check if victim died
                if (victim.Position == Position.Dead)
                {
                    await attacker.Session.SendLineAsync(
                        $"{victim.Name} is DEAD!!", cancellationToken);
                    CombatCalculator.StopFighting(player);
                    CombatCalculator.StopFighting(victimConnection.Player);
                }
            }
            else
            {
                // Mob victim - direct damage application
                var mobInstance = (MobInstance)victim;
                mobInstance.HitPoints -= (short)damage;
                
                if (mobInstance.HitPoints <= 0)
                {
                    mobInstance.Position = Position.Dead;
                }

                await attacker.Session.SendLineAsync(
                    $"Your kick hits {victim.Name} [{damage}]", cancellationToken);
                await BroadcastToRoomPvE(attacker,
                    $"{player.Name} kicks {victim.Name}!", cancellationToken);

                // Check if mob died
                if (mobInstance.Position == Position.Dead)
                {
                    await attacker.Session.SendLineAsync(
                        $"{victim.Name} is DEAD!!", cancellationToken);
                    
                    CombatCalculator.StopFighting(player);
                    mobInstance.FightingConnectionId = null;
                    
                    _worldState.CreateMobCorpse(mobInstance, player.RoomId);
                    _worldState.RemoveMob(mobInstance.InstanceId, player.RoomId);
                    
                    await BroadcastToRoomPvE(attacker,
                        $"{victim.Name} is dead! R.I.P.", cancellationToken);
                }
            }

            // Improve skill on successful hit
            player.TryImproveSkill(SkillType.Kick);
        }

        return CommandOutcome.Continue;
    }

    private async Task BroadcastToRoomPvP(
        ConnectionContext attacker,
        ConnectionContext victim,
        string message,
        CancellationToken cancellationToken)
    {
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId 
                     && c.Id != attacker.Id 
                     && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(message, cancellationToken);
        }
    }

    private async Task BroadcastToRoomPvE(
        ConnectionContext attacker,
        string message,
        CancellationToken cancellationToken)
    {
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId 
                     && c.Id != attacker.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(message, cancellationToken);
        }
    }
}
