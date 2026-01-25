using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Bash;

/// <summary>
/// Bash command - attempts to break down a door with brute force.
/// Requires shield equipped (currently not enforced).
/// Success based on SKILL_BASH + strength bonus vs random roll.
/// Failure causes 1-25 HP self-damage.
/// Legacy: do_bash() in act.offensive.c:484-583 (door logic at lines 504-547)
/// </summary>
[Command("bash")]
internal sealed class BashCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public BashCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument ?? string.Empty;

        if (string.IsNullOrWhiteSpace(argument))
        {
            await context.Session.SendLineAsync("Bash what?", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Try to find a door
        var doorResult = DoorFinder.FindDoor(_worldState, context.Player, argument);
        
        if (!doorResult.Found || doorResult.Direction == null || doorResult.Exit == null)
        {
            // TODO: In full implementation, also support bashing opponents (SKILL_BASH combat skill)
            // For now, only doors are supported
            if (doorResult.ErrorMessage != null)
            {
                await context.Session.SendLineAsync(doorResult.ErrorMessage, cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync("You don't see that here.", cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        return await HandleDoorBash(context, doorResult.Direction.Value, doorResult.Exit, cancellationToken);
    }
    
    /// <summary>
    /// Handle bashing a door.
    /// Legacy: do_bash() in act.offensive.c:504-547
    /// Formula: random(1, 1000) > (SKILL_BASH + str_app[str].bash)
    /// </summary>
    private async ValueTask<CommandOutcome> HandleDoorBash(
        ConnectionContext context,
        Direction direction,
        ExitDefinition exit,
        CancellationToken cancellationToken)
    {
        // Check if it's actually a door
        if (!exit.IsDoor)
        {
            await context.Session.SendLineAsync("That's not a door.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Get current door state
        var doorState = _worldState.GetDoorState(context.Player.RoomId, direction);
        if (doorState == null)
        {
            await context.Session.SendLineAsync("That's not a door.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if door is already open
        if (!doorState.IsClosed)
        {
            await context.Session.SendLineAsync("The door is already open.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if door is bashproof
        if (exit.Bashproof)
        {
            await context.Session.SendLineAsync("This door is too sturdy to bash.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // TODO: Check for shield equipped when equipment system is complete
        // Legacy: if (!ch->equipment[WEAR_SHIELD])
        // For now, we'll skip this check as mentioned in ParrySkill.cs line 52-54
        var equipment = _worldState.GetPlayerEquipment(context.Player);
        if (!equipment.ContainsKey(EquipmentSlot.Shield))
        {
            await context.Session.SendLineAsync("You need a shield equipped to bash doors.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Calculate bash success
        int bashSkill = context.Player.GetSkill(SkillType.Bash);
        int strengthBonus = GetStrengthBashBonus(context.Player.Strength);
        int totalBashPower = bashSkill + strengthBonus;
        
        // Legacy formula: if (number(1, 1000) > totalBashPower || IS_SET(EX_BASHPROOF))
        int roll = Random.Shared.Next(1, 1001); // 1-1000
        
        if (roll > totalBashPower)
        {
            // FAILURE - player takes damage
            await context.Session.SendLineAsync("You bounce off the door.", cancellationToken);
            
            // Take 1-25 HP damage
            int damage = Random.Shared.Next(1, 26);
            context.Player.HitPoints = (short)Math.Max(1, context.Player.HitPoints - damage);
            
            // TODO: Notify room when act() system is fully implemented
            // Legacy: act("$n crashes into the $F $T, and bounces off.", FALSE, ch, 0, door, TO_ROOM);
            
            return CommandOutcome.Continue;
        }
        else
        {
            // SUCCESS - door is broken open!
            _worldState.BreakDoor(context.Player.RoomId, direction);
            
            await context.Session.SendLineAsync("*smash*", cancellationToken);
            await context.Session.SendLineAsync("You bash the door off its hinges!", cancellationToken);
            
            // TODO: Notify room when act() system is fully implemented
            // Legacy: act("$n smashes the $F $T wide open!", FALSE, ch, 0, door, TO_ROOM);
            
            return CommandOutcome.Continue;
        }
    }
    
    /// <summary>
    /// Get bash bonus from strength.
    /// Legacy: str_app[STRENGTH_APPLY_INDEX(ch)].bash
    /// 
    /// Simplified table based on legacy values:
    /// Str 0-5:   -10
    /// Str 6-10:  -5
    /// Str 11-15: 0
    /// Str 16-17: 10
    /// Str 18:    20
    /// Str 19+:   30
    /// </summary>
    private int GetStrengthBashBonus(sbyte strength)
    {
        if (strength <= 5) return -10;
        if (strength <= 10) return -5;
        if (strength <= 15) return 0;
        if (strength <= 17) return 10;
        if (strength == 18) return 20;
        return 30; // 19+
    }
}
