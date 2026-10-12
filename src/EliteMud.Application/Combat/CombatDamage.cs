using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// The one place damage is dealt (legacy fight.c damage()). A damage source decides whether it hits and for how
/// much; everything after that happens here: fighting state, damage experience, and the victim's death.
/// </summary>
public sealed class CombatDamage
{
    private const int MaxDamage = 500;

    private readonly CombatCalculator _combatCalculator;
    private readonly CombatExperience _combatExperience;
    private readonly IWorldState _worldState;

    public CombatDamage(CombatCalculator combatCalculator, CombatExperience combatExperience, IWorldState worldState)
    {
        _combatCalculator = combatCalculator;
        _combatExperience = combatExperience;
        _worldState = worldState;
    }

    /// <summary>
    /// Deal damage from a player to a player or mob. A miss is damage 0: it still starts the fight.
    /// The messages cover what follows the source's own hit message (death, kill experience), in order.
    /// </summary>
    public DamageOutcome Apply(
        PlayerState attacker,
        int attackerConnectionId,
        ICombatant victim,
        int? victimConnectionId,
        int damage)
    {
        if (!ReferenceEquals(attacker, victim))
        {
            StartFighting(attacker, attackerConnectionId, victim, victimConnectionId);
        }

        return victimConnectionId != null
            ? DamagePlayer(attacker, (PlayerState)victim, damage)
            : DamageMob(attacker, (MobInstance)victim, damage);
    }

    /// <summary>Legacy set_fighting: each side that isn't fighting yet starts fighting the other.</summary>
    private void StartFighting(
        PlayerState attacker,
        int attackerConnectionId,
        ICombatant victim,
        int? victimConnectionId)
    {
        if (attacker.FightingConnectionId == null)
        {
            var target = victimConnectionId ?? -((MobInstance)victim).InstanceId;
            _combatCalculator.SetFighting(attacker, target);
        }

        if (victim is PlayerState { FightingConnectionId: null } victimPlayer)
        {
            _combatCalculator.SetFighting(victimPlayer, attackerConnectionId);
        }
        else if (victim is MobInstance { FightingConnectionId: null } mob)
        {
            mob.FightingConnectionId = attackerConnectionId;
        }
    }

    /// <summary>
    /// A player victim. No dodge/parry here: legacy only has them in the melee hit, before damage().
    /// Death is still only noticed, not handled (#42 step 4, #40).
    /// </summary>
    private DamageOutcome DamagePlayer(PlayerState attacker, PlayerState victim, int damage)
    {
        damage = Math.Clamp(damage, 0, MaxDamage);
        victim.HitPoints -= (short)damage;
        _combatExperience.DamageDealt(attacker, victim, damage); // none between players; shared path
        _combatCalculator.UpdatePosition(victim);

        // A miss never kills, as for mobs
        var messages = new List<SkillMessage>();
        var died = damage > 0 && victim.Position == Position.Dead;
        if (died)
        {
            _combatCalculator.StopFighting(attacker);
            _combatCalculator.StopFighting(victim);
            AddDeathMessages(messages, victim);
        }

        return new DamageOutcome(damage, died, messages);
    }

    private DamageOutcome DamageMob(PlayerState attacker, MobInstance mob, int damage)
    {
        damage = Math.Clamp(damage, 0, MaxDamage);
        mob.HitPoints -= (short)damage;
        _combatExperience.DamageDealt(attacker, mob, damage);

        // A miss never kills, even a mob already at 0 hp or below
        var messages = new List<SkillMessage>();
        if (damage == 0 || mob.HitPoints > 0)
        {
            return new DamageOutcome(damage, false, messages);
        }

        mob.Position = Position.Dead;
        _combatCalculator.StopFighting(attacker);
        mob.FightingConnectionId = null;

        // Legacy change_alignment() is silent
        var alignmentShift = _combatCalculator.CalculateAlignmentShift(attacker, mob, isPvP: false);
        _combatCalculator.ApplyAlignmentShift(attacker, alignmentShift);
        var killReward = _combatExperience.Killed(attacker, mob);

        AddDeathMessages(messages, mob);

        // Kill XP messages come after the death messages, as in legacy
        foreach (var line in killReward.Messages)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, line));
        }

        _worldState.CreateMobCorpse(mob, attacker.RoomId);
        _worldState.RemoveMob(mob.InstanceId, attacker.RoomId);

        return new DamageOutcome(damage, true, messages);
    }

    private static void AddDeathMessages(List<SkillMessage> messages, ICombatant victim)
    {
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N is DEAD!!", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N is dead!  R.I.P.", victim)); // fight.c:967
        if (victim is PlayerState)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You are dead!  Sorry...", victim)); // fight.c:968
        }
    }
}
