using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Kill;

internal sealed class KillCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public KillCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Kill;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var targetName = command.Argument?.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            await context.Session.SendLineAsync("Kill whom?", cancellationToken);
            return CommandOutcome.Continue;
        }

        var player = context.Player;

        // Check if already fighting
        if (player.FightingConnectionId != null)
        {
            await context.Session.SendLineAsync("You're already fighting!", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Try to find a mob in the room
        var mobs = _worldState.GetMobsInRoom(player.RoomId);
        var targetMob = mobs.FirstOrDefault(m =>
            m.Definition.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
            m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetMob == null)
        {
            // Try to find another player in the room
            var otherPlayers = _connectionRegistry.GetConnections()
                .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id)
                .ToList();

            var targetPlayer = otherPlayers.FirstOrDefault(c =>
                c.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));

            if (targetPlayer == null)
            {
                await context.Session.SendLineAsync("They aren't here.", cancellationToken);
                return CommandOutcome.Continue;
            }

            // PvP combat initiation
            await InitiateCombatWithPlayer(context, targetPlayer, cancellationToken);
            return CommandOutcome.Continue;
        }

        // PvE combat initiation (mob)
        await InitiateCombatWithMob(context, targetMob, cancellationToken);
        return CommandOutcome.Continue;
    }

    private async Task InitiateCombatWithPlayer(
        ConnectionContext attacker,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        // Set both to fighting
        CombatService.SetFighting(attacker.Player, victim.Id);
        CombatService.SetFighting(victim.Player, attacker.Id);

        // Broadcast "You attack" messages
        await attacker.Session.SendLineAsync(
            $"You attack {victim.Player.Name}!", cancellationToken);
        await victim.Session.SendLineAsync(
            $"{attacker.Player.Name} attacks you!", cancellationToken);

        // Broadcast to room
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId 
                     && c.Id != attacker.Id 
                     && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(
                $"{attacker.Player.Name} attacks {victim.Player.Name}!", cancellationToken);
        }

        // Perform initial attack
        var result = CombatService.PerformAttack(attacker.Player, victim.Player);
        
        // Format legacy combat messages
        var attackerMsg = CombatService.FormatCombatMessage(
            attacker.Player.Name,
            victim.Player.Name,
            result.Damage,
            victim.Player.MaxHitPoints,
            MessagePerspective.ToChar);
            
        var victimMsg = CombatService.FormatCombatMessage(
            attacker.Player.Name,
            victim.Player.Name,
            result.Damage,
            victim.Player.MaxHitPoints,
            MessagePerspective.ToVict);
        
        // Send messages
        await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
        await victim.Session.SendLineAsync(victimMsg, cancellationToken);
        
        // Broadcast to room if hit
        if (result.Hit)
        {
            var roomMsg = CombatService.FormatCombatMessage(
                attacker.Player.Name,
                victim.Player.Name,
                result.Damage,
                victim.Player.MaxHitPoints,
                MessagePerspective.ToRoom);
                
            foreach (var observer in otherPlayers)
            {
                await observer.Session.SendLineAsync(roomMsg, cancellationToken);
            }

            // Award experience
            attacker.Player.Experience += CombatService.CalculateExperienceGain(victim.Player, result.Damage);
        }
    }

    private async Task InitiateCombatWithMob(
        ConnectionContext attacker,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Set player to fighting
        CombatService.SetFighting(attacker.Player, -mob.InstanceId); // Negative ID for mobs

        // Set mob to fighting player
        mob.FightingConnectionId = attacker.Id;
        mob.Position = CombatService.POS_FIGHTING;

        // Broadcast "You attack" messages
        await attacker.Session.SendLineAsync(
            $"You attack {mob.Definition.ShortDescription}!", cancellationToken);

        // Broadcast to room
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId && c.Id != attacker.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(
                $"{attacker.Player.Name} attacks {mob.Definition.ShortDescription}!", cancellationToken);
        }

        // Perform initial attack
        int mobMaxHp = Math.Max(mob.HitPoints, mob.Definition.Level * 10);
        int damage = CombatService.CalculateBareDamage(attacker.Player);
        mob.HitPoints -= damage;
        
        // Format legacy combat messages
        var attackerMsg = CombatService.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription,
            damage,
            mobMaxHp,
            MessagePerspective.ToChar);
            
        await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
        
        // Broadcast to room
        var roomMsg = CombatService.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription,
            damage,
            mobMaxHp,
            MessagePerspective.ToRoom);
            
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMsg, cancellationToken);
        }
        
        // Award experience
        attacker.Player.Experience += mob.Definition.Level * damage / 2;
    }
}
