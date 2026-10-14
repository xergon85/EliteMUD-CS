using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// Legacy damage(): "Attempt to damage a corpse" (fight.c:787). A victim already dead is still being handled
/// elsewhere (e.g. the combat tick); damaging it again would give a second death.
/// </summary>
public sealed class IgnoreCorpse : IDamageStep
{
    public void Apply(DamageContext context)
    {
        if (context.Victim.Position != Position.Dead)
        {
            return;
        }

        Console.WriteLine(
            $"[CombatDamage] Attempt to damage a corpse: {context.Attacker.Name} -> {context.Victim.Name}");
        context.Damage = 0;
        context.Stopped = true;
    }
}
