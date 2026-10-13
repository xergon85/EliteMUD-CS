using EliteMud.Application.Combat;
using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Ai;

/// <summary>
/// Who a MOB_MEMORY mob remembers. It remembers a mortal who hurts it, to attack them on sight later
/// (<see cref="MobAiService"/>), and forgets a player it has killed. Legacy damage(), fight.c:824 and :1065.
/// </summary>
public sealed class MobMemory : ICombatListener
{
    public void Hurt(ICombatant attacker, ICombatant victim)
    {
        if (victim is MobInstance mob
            && HasMemory(mob)
            && attacker is PlayerState player
            && player.Level <= ExperienceService.MaxMortalLevel)
        {
            mob.RememberPlayer(player.Id);
        }
    }

    public void Killed(ICombatant killer, ICombatant victim)
    {
        if (killer is MobInstance mob && HasMemory(mob) && victim is PlayerState player)
        {
            mob.ForgetPlayer(player.Id);
        }
    }

    private static bool HasMemory(MobInstance mob) => mob.Definition.ParsedFlags.HasFlag(MobFlags.Memory);
}
