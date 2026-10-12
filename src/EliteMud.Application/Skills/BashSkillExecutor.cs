using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Shared;
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
    private readonly CombatDamage _combatDamage;

    public SkillType SkillType => SkillType.Bash;
    public TargetingMode Targeting => TargetingMode.CurrentFightTarget;

    public BashSkillExecutor(SkillRegistry skillRegistry, CombatDamage combatDamage)
    {
        _bashSkill = (BashSkill)skillRegistry.GetActiveSkill(SkillType.Bash);
        _combatDamage = combatDamage;
    }

    /// <summary>
    /// Execute bash against a target (player or mob).
    /// Rolls hit and knocks someone down; the shared damage path handles combat, death and experience.
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

        // Roll hit/miss
        var hit = _bashSkill.RollHit(attacker);
        if (!hit)
        {
            // Legacy: damage(ch, victim, 0, SKILL_BASH), then the attacker falls down
            _combatDamage.Apply(attacker, context.ActorConnectionId, victim, context.VictimConnectionId, 0);
            attacker.Position = Position.Sitting;

            // Attacker gets 2 rounds WAIT_STATE
            attacker.WaitState = CombatConstants.WaitStates.Bash;

            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "You try to bash $N but kiss the ground instead.", victim),
                new SkillMessage(SkillMessageTarget.Victim, "$n tries to bash you but fails."),
                new SkillMessage(SkillMessageTarget.Others, "$n tries to bash $N but ends up eating dirt.", victim));
        }

        // Success: victim falls down (and loses a round) before the damage, as legacy
        victim.Position = Position.Sitting;
        if (victim is PlayerState victimPlayer)
        {
            victimPlayer.WaitState = CombatConstants.PULSE_VIOLENCE;
        }

        var outcome = _combatDamage.Apply(
            attacker,
            context.ActorConnectionId,
            victim,
            context.VictimConnectionId,
            _bashSkill.CalculateDamage());

        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, $"You easily bash $N. [{outcome.Damage}]", victim),
            new(SkillMessageTarget.Victim, $"$n easily bashes you! [{outcome.Damage}]"),
            new(SkillMessageTarget.Others, "$n easily bashes $N!", victim)
        };
        messages.AddRange(outcome.Messages);

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
