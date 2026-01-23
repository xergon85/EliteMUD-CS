using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes rescue skill logic in the Application layer.
/// 
/// Rescue allows you to take over combat from an ally who is being attacked.
/// Legacy reference: act.offensive.c:597-642 (do_rescue)
/// 
/// Flow:
/// 1. Find the ally (victim) you want to rescue
/// 2. Find who is attacking that ally (attacker)
/// 3. Redirect combat: attacker now fights rescuer, ally is freed
/// 4. Roll for success/failure
/// 5. If success: swap combat targets, improve skill
/// 6. Apply WAIT_STATE
/// </summary>
[Command("rescue")]
public sealed class RescueSkillExecutor : ISkillExecutor
{
    private readonly ISkillHandler _rescueSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Rescue;
    public TargetingMode Targeting => TargetingMode.RequiredInRoom; // Must specify who to rescue

    public RescueSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _rescueSkill = skillRegistry.GetActiveSkill(SkillType.Rescue);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute rescue to protect an ally from their attacker.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var rescuer = context.Actor;
        var ally = context.Victim; // The person we're trying to rescue

        // Ally is required
        if (ally == null)
        {
            return SkillResult.Failed("Rescue who?");
        }

        // Can't rescue yourself
        if (ally == rescuer)
        {
            return SkillResult.Failed("What about fleeing instead?");
        }

        // Check if rescuer can use rescue
        if (!_rescueSkill.CanUse(rescuer))
        {
            return SkillResult.Failed(_rescueSkill.GetCannotUseMessage(rescuer));
        }

        // Can't rescue someone you're fighting
        // Legacy: if (ch->specials.fighting == victim)
        if (rescuer.FightingConnectionId != null)
        {
            // Check if rescuer is fighting the ally
            bool fightingAlly = context.VictimConnectionId != null && 
                                rescuer.FightingConnectionId == context.VictimConnectionId;
            
            if (!fightingAlly && ally is MobInstance allyMob && 
                rescuer.FightingConnectionId == -allyMob.InstanceId)
            {
                fightingAlly = true;
            }

            if (fightingAlly)
            {
                return SkillResult.Failed("How can you rescue someone you are trying to kill?");
            }
        }

        // Find who is attacking the ally
        // Legacy: for (tmp_ch = world[ch->in_room]->people; tmp_ch && (tmp_ch->specials.fighting != victim); ...)
        // 
        // Simplified implementation for now:
        // - If ally is a player, check their FightingConnectionId to see who they're fighting
        // - We assume bidirectional combat (if A fights B, then B fights A)
        // 
        // TODO: Full implementation would enumerate all entities in room to find attacker
        PlayerState? attackingPlayer = null;
        MobInstance? attackingMob = null;
        int? attackingConnectionId = null;

        if (context.VictimConnectionId != null)
        {
            // Ally is a player - find who is fighting them
            var allyPlayer = (PlayerState)ally;
            
            // Check if ally is currently fighting someone
            if (allyPlayer.FightingConnectionId != null)
            {
                var allyTargetId = allyPlayer.FightingConnectionId.Value;
                if (allyTargetId > 0)
                {
                    // Ally is fighting a player (PvP) - not supported yet
                    // TODO: Need IWorldState.GetPlayerByConnectionId or ConnectionRegistry
                    return SkillResult.Failed("You can only rescue from mobs right now.");
                }
                else
                {
                    // Ally is fighting a mob
                    var mobId = -allyTargetId;
                    var mobs = _worldState.GetMobsInRoom(rescuer.RoomId);
                    attackingMob = mobs.FirstOrDefault(m => m.InstanceId == mobId);
                }
            }
        }
        else
        {
            // Ally is a mob - can't rescue mobs yet
            // TODO: Implement mob rescue support
            return SkillResult.Failed("You can't rescue a mob.");
        }

        // Nobody is fighting the ally
        if (attackingPlayer == null && attackingMob == null)
        {
            return SkillResult.Failed($"But nobody is fighting {ally.Name}!");
        }

        // Roll for success
        if (!RescueSkill.RollSuccess(rescuer))
        {
            // Apply wait state even on failure
            rescuer.WaitState = CombatConstants.WaitStates.Rescue;
            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "You fail the rescue!")
            );
        }

        // Success! Redirect combat
        var messages = new List<SkillMessage>();

        // Stop all existing combat relationships
        // Legacy: stop_fighting calls for victim, tmp_ch, and ch
        if (context.VictimConnectionId != null)
        {
            var allyPlayer = (PlayerState)ally;
            _combatCalculator.StopFighting(allyPlayer);
        }
        else
        {
            ((MobInstance)ally).FightingConnectionId = null;
            if (((MobInstance)ally).Position == Position.Fighting)
                ((MobInstance)ally).Position = Position.Standing;
        }

        if (attackingPlayer != null)
        {
            _combatCalculator.StopFighting(attackingPlayer);
        }
        else if (attackingMob != null)
        {
            attackingMob.FightingConnectionId = null;
            if (attackingMob.Position == Position.Fighting)
                attackingMob.Position = Position.Standing;
        }

        if (rescuer.FightingConnectionId != null)
        {
            _combatCalculator.StopFighting(rescuer);
        }

        // Set new combat: rescuer <-> attacker
        // Legacy: set_fighting(ch, tmp_ch); set_fighting(tmp_ch, ch);
        if (attackingPlayer != null && attackingConnectionId != null)
        {
            _combatCalculator.SetFighting(rescuer, attackingConnectionId.Value);
            _combatCalculator.SetFighting(attackingPlayer, context.ActorConnectionId);
        }
        else if (attackingMob != null)
        {
            _combatCalculator.SetFighting(rescuer, -attackingMob.InstanceId);
            attackingMob.FightingConnectionId = context.ActorConnectionId;
        }

        // Send messages
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Banzai!  To the rescue..."));
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You are rescued by $n, you are confused!"));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$n heroically rescues $N!", ally));

        // Improve skill on successful rescue
        // Legacy: improve_skill(ch, SKILL_RESCUE) at end
        if (rescuer.TryImproveSkill(SkillType.Rescue))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - rescue - just improved!"));
        }

        // Apply combat lag (WAIT_STATE) - rescue takes 2 rounds
        // Legacy: WAIT_STATE from general combat, not explicitly stated in do_rescue
        rescuer.WaitState = CombatConstants.WaitStates.Rescue;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
