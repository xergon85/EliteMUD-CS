using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>Legacy die() for a mob: the corpse takes its things, the mob leaves the world.</summary>
public sealed class MobDeath : IDamageStep
{
    private readonly IWorldState _worldState;

    public MobDeath(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public void Apply(DamageContext context)
    {
        if (!context.VictimDied || context.Victim is not MobInstance mob)
        {
            return;
        }

        var roomId = RoomOf(context.Attacker, mob);
        _worldState.CreateMobCorpse(mob, roomId);
        _worldState.RemoveMob(mob.InstanceId, roomId);
    }

    /// <summary>
    /// A mob doesn't know its room, so it comes from the player in the fight; nothing deals damage between mobs yet.
    /// </summary>
    private static int RoomOf(ICombatant attacker, MobInstance victim) =>
        attacker is PlayerState player
            ? player.RoomId
            : throw new NotSupportedException(
                $"Damage from {attacker.Name} to {victim.Name}: a mob dying to a mob needs the mob's room");
}
