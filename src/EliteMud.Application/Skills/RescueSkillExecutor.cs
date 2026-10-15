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
    private readonly RescueSkill _rescueSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Rescue;
    public TargetingMode Targeting => TargetingMode.RequiredInRoom; // Must specify who to rescue

    public RescueSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _rescueSkill = (RescueSkill)skillRegistry.GetActiveSkill(SkillType.Rescue);
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
        if (rescuer.Fighting == ally)
        {
            return SkillResult.Failed("How can you rescue someone you are trying to kill?");
        }

        // Find who is attacking the ally
        // Legacy: for (tmp_ch = world[ch->in_room]->people; tmp_ch && (tmp_ch->specials.fighting != victim); ...)
        //
        // Simplified implementation for now: we assume bidirectional combat (if A fights B, then B fights A),
        // so the ally's own opponent is the attacker.
        // TODO: Full implementation would enumerate all entities in room to find attacker
        if (ally is not PlayerState allyPlayer)
        {
            // TODO: Implement mob rescue support
            return SkillResult.Failed("You can't rescue a mob.");
        }

        if (allyPlayer.Fighting is PlayerState)
        {
            // Ally is fighting a player (PvP) - not supported yet
            return SkillResult.Failed("You can only rescue from mobs right now.");
        }

        var attackingMob = allyPlayer.Fighting as MobInstance;
        if (attackingMob == null || !_worldState.GetMobsInRoom(rescuer.RoomId).Contains(attackingMob))
        {
            return SkillResult.Failed($"But nobody is fighting {ally.Name}!");
        }

        // Roll for success
        if (!_rescueSkill.RollSuccess(rescuer))
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
        CombatCalculator.StopFighting(allyPlayer);
        CombatCalculator.StopFighting(attackingMob);
        if (rescuer.Fighting != null)
        {
            CombatCalculator.StopFighting(rescuer);
        }

        // Set new combat: rescuer <-> attacker
        // Legacy: set_fighting(ch, tmp_ch); set_fighting(tmp_ch, ch);
        _combatCalculator.SetFighting(rescuer, attackingMob);
        _combatCalculator.SetFighting(attackingMob, rescuer);

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
