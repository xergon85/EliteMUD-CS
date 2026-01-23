using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes backstab skill logic in the Application layer.
/// 
/// Backstab is a devastating surprise attack that can only be used on unsuspecting victims.
/// Legacy reference: act.offensive.c:203-256 (do_backstab), fight.c:1519-1521 (damage multiplier)
/// 
/// Requirements:
/// - Victim must not be fighting anyone
/// - TODO: Should require piercing weapon (not yet implemented)
/// 
/// Mechanics:
/// - Hit check: random(1, 101) vs skill proficiency
/// - Damage multiplier: MIN(level/10 + 1, 5) (1x at low level, up to 5x at level 40+)
/// - WAIT_STATE: 3 rounds (6 seconds)
/// - Skill improvement: Only on successful hit
/// </summary>
[Command("backstab", Aliases = new[] { "bs" })]
public sealed class BackstabSkillExecutor : ISkillExecutor
{
    private readonly ISkillHandler _backstabSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Backstab;
    public TargetingMode Targeting => TargetingMode.RequiredInRoom; // Must specify target

    public BackstabSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _backstabSkill = skillRegistry.GetActiveSkill(SkillType.Backstab);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute backstab against a target (player or mob).
    /// Handles combat initiation, damage calculation, death, and skill improvement.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var attacker = context.Actor;
        var victim = context.Victim;

        // Victim is required for backstab
        if (victim == null)
        {
            return SkillResult.Failed("Backstab who?");
        }

        // Can't backstab yourself
        if (victim == attacker)
        {
            return SkillResult.Failed("How can you sneak up on yourself?");
        }

        // Check if player can use backstab
        if (!_backstabSkill.CanUse(attacker))
        {
            return SkillResult.Failed(_backstabSkill.GetCannotUseMessage(attacker));
        }

        // Legacy check: Can't backstab a fighting person (too alert)
        // Reference: act.offensive.c:241-244
        bool victimFighting = false;
        if (context.VictimConnectionId != null)
        {
            victimFighting = ((PlayerState)victim).FightingConnectionId != null;
        }
        else
        {
            victimFighting = ((MobInstance)victim).FightingConnectionId != null;
        }
        
        if (victimFighting)
        {
            return SkillResult.Failed("You can't backstab a fighting person, too alert!");
        }

        // TODO: Check for piercing weapon requirement
        // Legacy: ch->equipment[WIELD]->obj_flags.value[3] != 11
        // Reference: act.offensive.c:224-231

        // Roll hit/miss
        // If victim is asleep (position < fighting), auto-hit in legacy
        // Reference: act.offensive.c:248 (AWAKE(victim) && ...)
        bool victimAwake = victim.Position >= Position.Resting;
        bool hit = !victimAwake || BackstabSkill.RollHit(attacker);

        if (!hit)
        {
            // Miss - deal 0 damage but still start combat
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

            // Apply wait state even on miss
            attacker.WaitState = CombatConstants.WaitStates.Backstab;

            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "you try to backstab $N, but miss!", victim),
                new SkillMessage(SkillMessageTarget.Victim, "$n tries to backstab you, but misses!"),
                new SkillMessage(SkillMessageTarget.Others, "$n tries to backstab $N, but misses!", victim)
            );
        }

        // Calculate backstab damage
        // Legacy: dam = strength_bonus + damroll + weapon_dice
        // Then: dam *= MIN(level/10 + 1, 5)
        // For now, use simplified base damage (similar to kick) then multiply
        int baseDamage = _combatCalculator.CalculateBareDamage(attacker);
        int multiplier = BackstabSkill.CalculateDamageMultiplier(attacker);
        int damage = baseDamage * multiplier;

        // Apply damage
        var victimDied = false;
        var messages = new List<SkillMessage>();

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

        if (context.VictimConnectionId != null)
        {
            // Player victim - use CombatCalculator for dodge support
            var victimPlayer = (PlayerState)victim;
            var damageResult = _combatCalculator.ApplyDamage(victimPlayer, damage);

            damage = damageResult.Damage;

            // Show dodge message if dodged (though unlikely with backstab multiplier)
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
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, $"you backstab $N! [{damage}]", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, $"$n backstabs you! [{damage}]"));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n backstabs $N!", victim));

        // Add death messages if victim died
        if (victimDied)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N is DEAD!!", victim));
            messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N is dead! R.I.P.", victim));
        }

        // Improve skill on successful hit
        // Legacy: improve_skill(ch, SKILL_BACKSTAB) only on successful hit
        // Reference: act.offensive.c:252
        if (attacker.TryImproveSkill(SkillType.Backstab))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - backstab - just improved!"));
        }

        // Apply combat lag (WAIT_STATE) - backstab takes 3 rounds
        // Legacy: WAIT_STATE(ch, PULSE_VIOLENCE * 3) in act.offensive.c:254
        attacker.WaitState = CombatConstants.WaitStates.Backstab;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
