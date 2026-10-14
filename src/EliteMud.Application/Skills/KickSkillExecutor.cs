using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Shared;
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
    private readonly KickSkill _kickSkill;
    private readonly CombatDamage _combatDamage;

    public SkillType SkillType => SkillType.Kick;
    public TargetingMode Targeting => TargetingMode.CurrentFightTarget;

    public KickSkillExecutor(SkillRegistry skillRegistry, CombatDamage combatDamage)
    {
        _kickSkill = (KickSkill)skillRegistry.GetActiveSkill(SkillType.Kick);
        _combatDamage = combatDamage;
    }

    /// <summary>
    /// Execute kick against a target (player or mob).
    /// Rolls hit and damage; the shared damage path handles combat, death and experience.
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

        // Roll hit/miss
        var hit = _kickSkill.RollHit(attacker, victim);
        if (!hit)
        {
            // Legacy: damage(ch, victim, 0, SKILL_KICK) - a miss still starts the fight
            var miss = _combatDamage.Apply(attacker, victim, 0); // a miss can still make them wimp out or bleed

            // Apply combat lag even on miss (legacy: WAIT_STATE applied after damage())
            attacker.WaitState = CombatConstants.WaitStates.Kick;
            
            return SkillResult.Succeeded(new[]
            {
                new SkillMessage(SkillMessageTarget.Actor, "you try to kick $N, but miss!", victim),
                new SkillMessage(SkillMessageTarget.Victim, "$n tries to kick you, but misses!"),
                new SkillMessage(SkillMessageTarget.Others, "$n tries to kick $N, but misses!", victim)
            }.Concat(miss.Messages).ToArray());
        }

        var outcome = _combatDamage.Apply(
            attacker,
            victim,
            _kickSkill.CalculateDamage(attacker));

        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, $"your kick hits $N [{outcome.Damage}]", victim),
            new(SkillMessageTarget.Victim, $"$n kicks you! [{outcome.Damage}]"),
            new(SkillMessageTarget.Others, "$n kicks $N!", victim)
        };
        messages.AddRange(outcome.Messages);

        // Improve skill on successful hit
        if (attacker.TryImproveSkill(SkillType.Kick))
        {
            // Legacy: "Your skill - kick - just improved."
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - kick - just improved!"));
        }
        
        // Apply combat lag (WAIT_STATE) - kick takes 3 rounds
        // Legacy: WAIT_STATE(ch, PULSE_VIOLENCE * 3) in act.offensive.c:689
        attacker.WaitState = CombatConstants.WaitStates.Kick;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
