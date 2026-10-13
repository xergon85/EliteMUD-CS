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

    public SkillType SkillType => SkillType.Kick; // Not actually a skill, just needs a value
    public TargetingMode Targeting => TargetingMode.RequiredInRoom;

    public MeleeAttackExecutor(
        CombatCalculator combatCalculator,
        IWorldState worldState,
        MobAiService mobAiService,
        CombatDamage combatDamage)
    {
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

        // Get wielded weapon for damage calculation
        ObjectInstance? weapon = null;
        if (attacker.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var weaponInstanceId))
        {
            weapon = _worldState.GetObjectInstance(weaponInstanceId);
        }

        var weaponDetails = weapon?.Definition.Details?.Weapon;

        // Calculate effective stats (base + equipment + spell bonuses)
        var attackerEffectiveStr = _worldState.GetTotalEffectiveStrength(attacker);

        return context.VictimConnectionId != null
            ? AttackPlayer(context, (PlayerState)victim, attackerEffectiveStr, weaponDetails)
            : AttackMob(context, (MobInstance)victim, attackerEffectiveStr, weapon, weaponDetails);
    }

    private SkillResult AttackPlayer(
        SkillContext context,
        PlayerState victimPlayer,
        sbyte attackerEffectiveStr,
        ObjectWeapon? weaponDetails)
    {
        var attacker = context.Actor;
        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, "You attack $N!", victimPlayer),
            new(SkillMessageTarget.Victim, "$n attacks you!"),
            new(SkillMessageTarget.Others, "$n attacks $N!", victimPlayer)
        };

        // Hit roll, damage and the victim's dodge/parry, before the fight starts (legacy hit(), then damage())
        var result = _combatCalculator.ResolveAttack(
            attacker,
            attackerEffectiveStr,
            _worldState.GetTotalEffectiveIntelligence(attacker),
            _worldState.GetTotalEffectiveWisdom(attacker),
            victimPlayer,
            _worldState.GetTotalEffectiveDexterity(victimPlayer),
            weaponDetails);

        var outcome = _combatDamage.Apply(
            attacker,
            context.ActorConnectionId,
            victimPlayer,
            context.VictimConnectionId,
            result.Hit ? result.Damage : 0);

        // Format combat messages (legacy format with damage/health)
        var victimEffectiveMaxHp = _worldState.GetTotalEffectiveMaxHitPoints(victimPlayer);
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToChar)));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToVict)));

        if (result.Hit)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Others, CombatMessageFormatter.FormatCombatMessage(
                attacker.Name, victimPlayer.Name, outcome.Damage, victimEffectiveMaxHp, MessagePerspective.ToRoom)));
        }

        messages.AddRange(outcome.Messages);
        return new SkillResult(Success: true, Messages: messages.ToArray());
    }

    /// <summary>The first hit on a mob always lands for now (#48).</summary>
    private SkillResult AttackMob(
        SkillContext context,
        MobInstance mobInstance,
        sbyte attackerEffectiveStr,
        ObjectInstance? weapon,
        ObjectWeapon? weaponDetails)
    {
        var attacker = context.Actor;
        var mobDesc = mobInstance.Definition.ShortDescription?.Trim() ?? "something";
        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, $"You attack {mobDesc}!"),
            new(SkillMessageTarget.Others, $"$n attacks {mobDesc}!")
        };

        int damage = _combatCalculator.CalculateDamage(attacker, attackerEffectiveStr, weaponDetails);

        // Apply weapon special effects (bless/evil/flame)
        if (weapon != null)
        {
            damage += _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, mobInstance);
        }

        mobInstance.Position = Position.Fighting;
        var outcome = _combatDamage.Apply(
            attacker,
            context.ActorConnectionId,
            mobInstance,
            context.VictimConnectionId,
            damage);

        // Trigger MOB_HELPER mobs to assist this mob, after the hit (legacy autoassist(), fight.c:1725);
        // a mob that just died has no fight left to join
        if (!outcome.VictimDied)
        {
            var emptyConnections = new Dictionary<int, PlayerConnection>();
            _mobAiService.ProcessAssist(mobInstance, attacker.RoomId, _worldState, emptyConnections);
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
