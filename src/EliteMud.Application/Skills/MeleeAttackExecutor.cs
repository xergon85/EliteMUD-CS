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
public sealed class MeleeAttackExecutor : ISkillExecutor
{
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Kick; // Not actually a skill, just needs a value
    public CommandKind CommandKind => CommandKind.Kill;
    public TargetingMode Targeting => TargetingMode.RequiredInRoom;

    public MeleeAttackExecutor(
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute melee attack to initiate combat.
    /// Handles combat initiation, initial attack, and experience gain.
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

        var messages = new List<SkillMessage>();

        // Initiate combat
        if (context.VictimConnectionId != null)
        {
            // PvP combat
            var victimPlayer = (PlayerState)victim;
            _combatCalculator.SetFighting(attacker, context.VictimConnectionId.Value);
            _combatCalculator.SetFighting(victimPlayer, context.ActorConnectionId);

            // Attack messages
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "You attack $N!", victim));
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, "$n attacks you!"));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n attacks $N!", victim));

            // Perform initial attack
            var result = _combatCalculator.PerformAttack(attacker, victimPlayer);

            // Format combat messages (legacy format with damage/health)
            var attackerCombatMsg = CombatMessageFormatter.FormatCombatMessage(
                attacker.Name,
                victimPlayer.Name,
                result.Damage,
                victimPlayer.MaxHitPoints,
                MessagePerspective.ToChar);

            var victimCombatMsg = CombatMessageFormatter.FormatCombatMessage(
                attacker.Name,
                victimPlayer.Name,
                result.Damage,
                victimPlayer.MaxHitPoints,
                MessagePerspective.ToVict);

            messages.Add(new SkillMessage(SkillMessageTarget.Actor, attackerCombatMsg));
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, victimCombatMsg));

            if (result.Hit)
            {
                var roomCombatMsg = CombatMessageFormatter.FormatCombatMessage(
                    attacker.Name,
                    victimPlayer.Name,
                    result.Damage,
                    victimPlayer.MaxHitPoints,
                    MessagePerspective.ToRoom);

                messages.Add(new SkillMessage(SkillMessageTarget.Others, roomCombatMsg));

                // Award experience
                attacker.Experience += _combatCalculator.CalculateExperienceGain(victimPlayer, result.Damage);
            }
        }
        else
        {
            // PvE combat (mob)
            var mobInstance = (MobInstance)victim;
            _combatCalculator.SetFighting(attacker, -mobInstance.InstanceId);
            mobInstance.FightingConnectionId = context.ActorConnectionId;
            mobInstance.Position = Position.Fighting;

            var mobDesc = mobInstance.Definition.ShortDescription?.Trim() ?? "something";

            // Attack messages
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, $"You attack {mobDesc}!"));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, $"$n attacks {mobDesc}!"));

            // Perform initial attack
            int mobMaxHp = mobInstance.Definition.MaxHitPoints;
            int damage = _combatCalculator.CalculateBareDamage(attacker);
            mobInstance.HitPoints -= (short)damage;

            // Format combat messages
            var attackerCombatMsg = CombatMessageFormatter.FormatCombatMessage(
                attacker.Name,
                mobDesc,
                damage,
                mobMaxHp,
                MessagePerspective.ToChar);

            var roomCombatMsg = CombatMessageFormatter.FormatCombatMessage(
                attacker.Name,
                mobDesc,
                damage,
                mobMaxHp,
                MessagePerspective.ToRoom);

            messages.Add(new SkillMessage(SkillMessageTarget.Actor, attackerCombatMsg));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, roomCombatMsg));

            // Award experience
            attacker.Experience += mobInstance.Definition.Level * damage / 2;
        }

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
