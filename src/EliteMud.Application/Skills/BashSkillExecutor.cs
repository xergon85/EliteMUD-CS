using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes bash skill logic in the Application layer.
/// 
/// Bash is a shield attack that knocks the opponent down on success,
/// or knocks the attacker down on failure.
/// 
/// Legacy: act.offensive.c:484-583 (do_bash)
/// </summary>
[Command("bash")]
public sealed class BashSkillExecutor : ISkillExecutor
{
    private readonly BashSkill _bashSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Bash;
    public TargetingMode Targeting => TargetingMode.CurrentFightTarget;

    public BashSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _bashSkill = (BashSkill)skillRegistry.GetActiveSkill(SkillType.Bash);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute bash against a target (player or mob).
    /// Handles combat initiation, position changes, damage, death, and skill improvement.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var attacker = context.Actor;
        var victim = context.Victim;

        // Victim is required for bash
        if (victim == null)
        {
            return SkillResult.Failed("Bash who?");
        }

        // Check if player can use bash
        if (!_bashSkill.CanUse(attacker))
        {
            return SkillResult.Failed(_bashSkill.GetCannotUseMessage(attacker));
        }

        // TODO: Check if attacker has shield equipped
        // For now, skip shield requirement for POC

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
        var hit = _bashSkill.RollHit(attacker);
        var messages = new List<SkillMessage>();

        if (!hit)
        {
            // Failure: Attacker falls down
            attacker.Position = Position.Sitting;

            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "You try to bash $N but kiss the ground instead.", victim));
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, "$n tries to bash you but fails."));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n tries to bash $N but ends up eating dirt.", victim));

            // Apply 0 damage (triggers combat but no damage)
            if (context.VictimConnectionId != null)
            {
                var victimPlayer = (PlayerState)victim;
                _combatCalculator.ApplyDamage(victimPlayer, 0);
            }
            else
            {
                // Mob victim - no damage
            }

            // Attacker gets 2 rounds WAIT_STATE
            attacker.WaitState = CombatConstants.WaitStates.Bash;

            return new SkillResult(Success: true, Messages: messages.ToArray());
        }

        // Success: Victim falls down and takes damage
        var damage = _bashSkill.CalculateDamage();
        var victimDied = false;

        // Knock victim down
        victim.Position = Position.Sitting;

        // Apply damage
        if (context.VictimConnectionId != null)
        {
            // Player victim - use CombatCalculator
            var victimPlayer = (PlayerState)victim;
            var damageResult = _combatCalculator.ApplyDamage(victimPlayer, damage);

            damage = damageResult.Damage;
            victimDied = victimPlayer.Position == Position.Dead;

            // Apply victim WAIT_STATE (1 round)
            victimPlayer.WaitState = CombatConstants.PULSE_VIOLENCE;

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

            // Apply victim WAIT_STATE (mobs don't track this in POC)
            // mobInstance.WaitState = CombatConstants.PULSE_VIOLENCE;

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
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, $"You easily bash $N. [{damage}]", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, $"$n easily bashes you! [{damage}]"));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n easily bashes $N!", victim));

        // Add death messages if victim died
        if (victimDied)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N is DEAD!!", victim));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N is dead! R.I.P.", victim));
        }

        // Improve skill on successful hit
        if (attacker.TryImproveSkill(SkillType.Bash))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - bash - just improved!"));
        }

        // Apply combat lag (WAIT_STATE) - bash takes 2 rounds
        attacker.WaitState = CombatConstants.WaitStates.Bash;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
