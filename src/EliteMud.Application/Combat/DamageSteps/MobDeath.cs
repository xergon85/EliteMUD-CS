using EliteMud.Application.World;

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

        _worldState.CreateMobCorpse(mob, mob.RoomId);
        _worldState.RemoveMob(mob.InstanceId, mob.RoomId);
    }
}
