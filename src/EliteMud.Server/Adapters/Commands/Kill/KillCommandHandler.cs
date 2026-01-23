using EliteMud.Application.Combat;
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
        var player = context.Player;

        // Validate preconditions
        var validationResult = KillCommandValidator.Validate(player, targetName);
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Try to find a mob in the room (supports "2.soldier" syntax)
        // Legacy: handler.c:1481-1501 (get_char_room_vis uses get_number)
        var (index, name) = TargetParser.ParseTarget(targetName!); // targetName validated above
        if (index == 0)
        {
            await context.Session.SendLineAsync("Invalid target format.", cancellationToken);
            return CommandOutcome.Continue;
        }

        var mobs = _worldState.GetMobsInRoom(player.RoomId);
        var targetMob = TargetParser.FindNthMatch(mobs, name, index);

        if (targetMob == null)
        {
            // Try to find another player in the room
            var otherPlayers = _connectionRegistry.GetConnections()
                .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id)
                .ToList();

            var targetPlayer = otherPlayers.FirstOrDefault(c =>
                c.Player.Name.Contains(targetName!, StringComparison.OrdinalIgnoreCase)); // targetName validated above

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
        CombatCalculator.SetFighting(attacker.Player, victim.Id);
        CombatCalculator.SetFighting(victim.Player, attacker.Id);

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
        var result = CombatCalculator.PerformAttack(attacker.Player, victim.Player);
        
        // Format legacy combat messages
        var attackerMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            victim.Player.Name,
            result.Damage,
            victim.Player.MaxHitPoints,
            MessagePerspective.ToChar);
            
        var victimMsg = CombatMessageFormatter.FormatCombatMessage(
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
            var roomMsg = CombatMessageFormatter.FormatCombatMessage(
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
            attacker.Player.Experience += CombatCalculator.CalculateExperienceGain(victim.Player, result.Damage);
        }
    }

    private async Task InitiateCombatWithMob(
        ConnectionContext attacker,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Set player to fighting
        CombatCalculator.SetFighting(attacker.Player, -mob.InstanceId); // Negative ID for mobs

        // Set mob to fighting player
        mob.FightingConnectionId = attacker.Id;
        mob.Position = Position.Fighting;

        // Broadcast "You attack" messages
        var mobDesc = mob.Definition.ShortDescription?.Trim() ?? "something";
        await attacker.Session.SendLineAsync(
            $"You attack {mobDesc}!", cancellationToken);

        // Broadcast to room
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId && c.Id != attacker.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(
                $"{attacker.Player.Name} attacks {mobDesc}!", cancellationToken);
        }

        // Perform initial attack
        int mobMaxHp = mob.Definition.MaxHitPoints;
        int damage = CombatCalculator.CalculateBareDamage(attacker.Player);
        mob.HitPoints -= (short)damage;
        
        // Format legacy combat messages
        var attackerMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription ?? "something",
            damage,
            mobMaxHp,
            MessagePerspective.ToChar);
            
        await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
        
        // Broadcast to room
        var roomMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription ?? "something",
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
