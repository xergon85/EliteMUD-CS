using EliteMud.Application.Skills;

namespace EliteMud.Application.Combat;

/// <summary>
/// What a hit did: the damage actually dealt (after dodge/parry and caps), whether the victim died, and the
/// messages that follow the source's own hit message.
/// </summary>
public sealed record DamageOutcome(int Damage, bool VictimDied, IReadOnlyList<SkillMessage> Messages);
