namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>Tells every <see cref="ICombatListener"/> who hurt whom, e.g. for mob memory. A miss counts.</summary>
public sealed class NotifyHurt : IDamageStep
{
    private readonly IReadOnlyList<ICombatListener> _listeners;

    public NotifyHurt(IReadOnlyList<ICombatListener> listeners)
    {
        _listeners = listeners;
    }

    public void Apply(DamageContext context)
    {
        if (context.SelfInflicted)
        {
            return;
        }

        foreach (var listener in _listeners)
        {
            listener.Hurt(context.Attacker, context.Victim);
        }
    }
}
