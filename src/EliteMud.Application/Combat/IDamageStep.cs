namespace EliteMud.Application.Combat;

/// <summary>
/// One part of legacy damage(), e.g. the cap or the victim's death. <see cref="CombatDamage"/> runs its steps in one
/// fixed order; adding a rule is one step and one line in that list.
/// </summary>
public interface IDamageStep
{
    void Apply(DamageContext context);
}
