using EliteMud.Application.Ai;
using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes basic melee attack (kill command) in the Application layer.
/// Contains all business logic for initiating combat and performing first attack.
/// 
/// This is NOT a "skill" in the traditional sense (no skill proficiency),
/// but uses ISkillExecutor for consistency and to leverage generic command routing.
/// 
/// Legacy: do_kill() in act.offensive.c
/// </summary>
[Command("kill", Aliases = new[] { "k", "hit" })]
public sealed class MeleeAttackExecutor : ISkillExecutor
{
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;
    private readonly MobAiService _mobAiService;
    private readonly CombatDamage _combatDamage;
    private readonly MeleeRoll _meleeRoll;
    private readonly EffectiveStats _stats;

    public SkillType SkillType => SkillType.Kick; // Not actually a skill, just needs a value
    public TargetingMode Targeting => TargetingMode.RequiredInRoom;

    public MeleeAttackExecutor(
        CombatCalculator combatCalculator,
        IWorldState worldState,
        MobAiService mobAiService,
        CombatDamage combatDamage,
        MeleeRoll meleeRoll,
        EffectiveStats stats)
    {
        _stats = stats;
        _meleeRoll = meleeRoll;
        _combatCalculator = combatCalculator;
        _worldState = worldState;
        _mobAiService = mobAiService;
        _combatDamage = combatDamage;
    }

    /// <summary>
    /// Execute melee attack to initiate combat.
    /// Resolves the first hit (legacy hit()); the shared damage path starts the fight and handles death.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var attacker = context.Actor;
        var victim = context.Victim;

        // Victim is required
        if (victim == null)
        {
            return SkillResult.Failed("Kill who?");
        }

        // Validate can attack
        if (attacker.Position < Position.Fighting)
        {
            return SkillResult.Failed("You can't attack while sitting down!");
        }

        // Out of movement, there's no attack at all, so no fight starts either (legacy hit())
        if (_meleeRoll.Winded(attacker) is { } panting)
        {
            return new SkillResult(Success: true, Messages: panting.ToArray());
        }

        return victim is PlayerState victimPlayer
            ? AttackPlayer(context, victimPlayer)
            : AttackMob(context, (MobInstance)victim);
    }

    private SkillResult AttackPlayer(SkillContext context, PlayerState victimPlayer)
    {
        var attacker = context.Actor;
        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, "You attack $N!", victimPlayer),
            new(SkillMessageTarget.Victim, "$n attacks you!"),
            new(SkillMessageTarget.Others, "$n attacks $N!", victimPlayer)
        };

        // Hit roll, damage and the victim's dodge/parry, before the fight starts (legacy hit(), then damage())
        var hit = _meleeRoll.Hits(attacker, victimPlayer);
        var damage = hit
            ? _combatCalculator.DefendAgainstMelee(victimPlayer, _meleeRoll.Damage(attacker, victimPlayer)).Damage
            : 0;

        // Before the hit: a death moves the victim's gear, and its bonuses, into the corpse
        var victimEffectiveMaxHp = _stats.Get(victimPlayer, AffectLocation.MaxHit);
        var outcome = _combatDamage.Apply(attacker, victimPlayer, damage);

        // Format combat messages (legacy format with damage/health)
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToChar)));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToVict)));

        if (hit)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Others, CombatMessageFormatter.FormatCombatMessage(
                attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToRoom)));
        }

        messages.AddRange(outcome.Messages);
        return new SkillResult(Success: true, Messages: messages.ToArray());
    }

    /// <summary>The first hit on a mob, rolled to hit as any melee hit (#48).</summary>
    private SkillResult AttackMob(SkillContext context, MobInstance mobInstance)
    {
        var attacker = context.Actor;
        var mobDesc = mobInstance.Definition.ShortDescription?.Trim() ?? "something";
        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, $"You attack {mobDesc}!"),
            new(SkillMessageTarget.Others, $"$n attacks {mobDesc}!")
        };

        // Hit roll, damage and the mob's defense, as for any melee hit
        var damage = _meleeRoll.Hits(attacker, mobInstance)
            ? _combatCalculator.DefendAgainstMelee(mobInstance, _meleeRoll.Damage(attacker, mobInstance)).Damage
            : 0;

        var outcome = _combatDamage.Apply(
            attacker,
            mobInstance,
            damage);

        // Trigger MOB_HELPER mobs to assist this mob, after the hit (legacy autoassist(), fight.c:1725);
        // a mob that just died has no fight left to join
        if (!outcome.VictimDied)
        {
            _mobAiService.ProcessAssist(mobInstance, attacker.RoomId, _worldState);
        }

        // Format combat messages
        int mobMaxHp = mobInstance.Definition.MaxHitPoints;
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, mobDesc, outcome.Damage, mobMaxHp, MessagePerspective.ToChar)));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, mobDesc, outcome.Damage, mobMaxHp, MessagePerspective.ToRoom)));

        messages.AddRange(outcome.Messages);
        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
