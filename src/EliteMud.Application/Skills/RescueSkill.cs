using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Rescue skill - switch combat targets to protect an ally.
/// Legacy reference: act.offensive.c:597-642 (do_rescue)
/// 
/// Mechanics:
/// - Rescuer takes over combat from ally who is being attacked
/// - Stops ally's combat, redirects attacker to rescuer
/// - Both rescuer and attacker become fighting each other
/// </summary>
public sealed class RescueSkill : ISkillHandler
{
    public SkillType SkillType => SkillType.Rescue;
    public string Name => "Rescue";
    public string Description => "Heroically take over combat to protect an ally being attacked.";
    public int MinimumLevel => 1;
    public int WaitStateRounds => CombatConstants.WaitStates.Rescue;

    public bool CanUse(ICombatant user)
    {
        // Must have skill proficiency
        if (user.GetSkill(SkillType) == 0)
            return false;

        // Must be in standing or fighting position (not sitting/resting/sleeping)
        if (user.Position < Position.Fighting)
            return false;

        return true;
    }

    public string GetCannotUseMessage(ICombatant user)
    {
        if (user.GetSkill(SkillType) == 0)
            return "You don't know how to rescue.";

        if (user.Position < Position.Fighting)
            return "You can't rescue from this position.";

        return "You can't rescue right now.";
    }

    /// <summary>
    /// Roll to see if rescue succeeds.
    /// Legacy formula: random(1, 101) vs skill_percent
    /// Reference: act.offensive.c:625-626
    /// </summary>
    public static bool RollSuccess(ICombatant rescuer)
    {
        var roll = Random.Shared.Next(1, 102); // 1-101
        var skillPercent = rescuer.GetSkill(SkillType.Rescue);
        return roll <= skillPercent;
    }
}
