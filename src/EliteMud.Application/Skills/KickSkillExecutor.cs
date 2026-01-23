using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes kick skill logic in the Application layer.
/// Contains all business logic for kick execution, separate from infrastructure concerns.
/// 
/// This follows clean architecture:
/// - Application layer (this class): Business logic, skill execution
/// - Game layer (KickSkill): Pure domain logic (formulas, calculations)
/// - Server layer (generic SkillCommandHandler): Routing, message formatting
/// </summary>
[Command("kick")]
public sealed class KickSkillExecutor : ISkillExecutor
{
    private readonly ISkillHandler _kickSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Kick;
    public CommandKind CommandKind => CommandKind.Kick;
    public TargetingMode Targeting => TargetingMode.CurrentFightTarget;

    public KickSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _kickSkill = skillRegistry.GetActiveSkill(SkillType.Kick);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute kick against a target (player or mob).
    /// Handles combat initiation, damage calculation, death, and skill improvement.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var attacker = context.Actor;
        var victim = context.Victim;
        
        // Victim is required for kick
        if (victim == null)
        {
            return SkillResult.Failed("Kick who?");
        }

        // Check if player can use kick
        if (!_kickSkill.CanUse(attacker))
        {
            return SkillResult.Failed(_kickSkill.GetCannotUseMessage(attacker));
        }

        // Start combat if not already fighting
        if (attacker.FightingConnectionId == null)
        {
            if (context.VictimConnectionId != null)
            {
                // PvP - both players fight each other
                var victimPlayer = (PlayerState)victim;
                _combatCalculator.SetFighting(attacker, context.VictimConnectionId.Value);
                _combatCalculator.SetFighting(victimPlayer, context.ActorConnectionId);
            }
            else
            {
                // PvE - player fights mob
                var mobInstance = (MobInstance)victim;
                _combatCalculator.SetFighting(attacker, -mobInstance.InstanceId);
                mobInstance.FightingConnectionId = context.ActorConnectionId;
            }
        }

        // Roll hit/miss
        var hit = KickSkill.RollHit(attacker, victim);
        if (!hit)
        {
            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "you try to kick $N, but miss!", victim),
                new SkillMessage(SkillMessageTarget.Victim, "$n tries to kick you, but misses!"),
                new SkillMessage(SkillMessageTarget.Others, "$n tries to kick $N, but misses!", victim)
            );
        }

        // Calculate damage
        var damage = KickSkill.CalculateDamage(attacker);

        // Apply damage
        var victimDied = false;
        var messages = new List<SkillMessage>();

        if (context.VictimConnectionId != null)
        {
            // Player victim - use CombatCalculator for dodge support
            var victimPlayer = (PlayerState)victim;
            var damageResult = _combatCalculator.ApplyDamage(victimPlayer, damage);

            damage = damageResult.Damage;
            
            // Show dodge message if dodged
            if (damageResult.Dodged && !string.IsNullOrEmpty(damageResult.Message))
            {
                messages.Add(new SkillMessage(SkillMessageTarget.Victim, damageResult.Message));
            }

            victimDied = victimPlayer.Position == Position.Dead;

            // Stop fighting if victim died
            if (victimDied)
            {
                _combatCalculator.StopFighting(attacker);
                _combatCalculator.StopFighting(victimPlayer);
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
                victimDied = true;

                // Stop fighting and clean up mob
                _combatCalculator.StopFighting(attacker);
                mobInstance.FightingConnectionId = null;

                _worldState.CreateMobCorpse(mobInstance, attacker.RoomId);
                _worldState.RemoveMob(mobInstance.InstanceId, attacker.RoomId);
            }
        }

        // Add hit messages
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, $"your kick hits $N [{damage}]", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, $"$n kicks you! [{damage}]"));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n kicks $N!", victim));

        // Add death messages if victim died
        if (victimDied)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N is DEAD!!", victim));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N is dead! R.I.P.", victim));
        }

        // Improve skill on successful hit
        attacker.TryImproveSkill(SkillType.Kick);

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
