using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// The killer's side of a death (legacy damage() at POS_DEAD): the fight ends, alignment moves (silent,
/// change_alignment()), kill experience comes from the victim's experience before any death penalty, then the death
/// messages. Killing yourself earns nothing: legacy only rewards when ch != victim (fight.c:1034). A mob killer
/// takes its share of experience; mob alignment doesn't change here.
/// </summary>
public sealed class Kill : IDamageStep
{
    private readonly CombatCalculator _combatCalculator;
    private readonly CombatExperience _combatExperience;
    private readonly IReadOnlyList<ICombatListener> _listeners;

    public Kill(
        CombatCalculator combatCalculator,
        CombatExperience combatExperience,
        IReadOnlyList<ICombatListener> listeners)
    {
        _combatCalculator = combatCalculator;
        _combatExperience = combatExperience;
        _listeners = listeners;
    }

    public void Apply(DamageContext context)
    {
        if (!context.VictimDied)
        {
            return;
        }

        var killer = context.Attacker;
        var victim = context.Victim;
        var messages = context.Messages;
        _combatCalculator.StopFighting(killer);
        _combatCalculator.StopFighting(victim);

        IReadOnlyList<string> killReward = Array.Empty<string>();
        if (!context.SelfInflicted)
        {
            if (killer is PlayerState player)
            {
                var alignmentShift = _combatCalculator.CalculateAlignmentShift(
                    player, victim, isPvP: victim is PlayerState);
                _combatCalculator.ApplyAlignmentShift(player, alignmentShift);
                player.SaveRequested = true; // legacy PLR_SAVECHR (fight.c:1047)
            }

            killReward = _combatExperience.Killed(killer, victim).Messages;
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N is DEAD!!", victim));
        }

        foreach (var listener in _listeners)
        {
            listener.Killed(killer, victim);
        }

        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N is dead!  R.I.P.", victim)); // fight.c:967
        if (victim is PlayerState)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You are dead!  Sorry...", victim)); // fight.c:968
        }

        // Kill XP messages come after the death messages, as in legacy
        foreach (var line in killReward)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, line));
        }
    }
}
