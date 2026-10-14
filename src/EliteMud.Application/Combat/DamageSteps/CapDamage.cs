namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>Legacy damage(): 0 to 500 ("Maxdam changed to 500 - Petrus").</summary>
public sealed class CapDamage : IDamageStep
{
    public const int MaxDamage = 500;

    public void Apply(DamageContext context)
    {
        context.Damage = Math.Clamp(context.Damage, 0, MaxDamage);
    }
}
