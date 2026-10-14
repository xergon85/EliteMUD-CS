using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// Legacy die() for a player: a corpse with their things where they fell, the death penalty, and back to the
/// temple with full hit points, mana and movement.
/// </summary>
public sealed class PlayerDeath : IDamageStep
{
    private readonly CombatExperience _combatExperience;
    private readonly IWorldState _worldState;

    public PlayerDeath(CombatExperience combatExperience, IWorldState worldState)
    {
        _combatExperience = combatExperience;
        _worldState = worldState;
    }

    public void Apply(DamageContext context)
    {
        if (!context.VictimDied || context.Victim is not PlayerState victim)
        {
            return;
        }

        _worldState.CreatePlayerCorpse(victim, victim.RoomId);
        var penalty = _combatExperience.Died(victim);

        victim.HitPoints = _worldState.GetTotalEffectiveMaxHitPoints(victim);
        victim.Mana = _worldState.GetTotalEffectiveMaxMana(victim);
        victim.Movement = _worldState.GetTotalEffectiveMaxMovement(victim);
        victim.Position = Position.Standing;
        victim.RoomId = CharacterStart.StartRoom;
        victim.SaveRequested = true;

        foreach (var line in penalty.Messages)
        {
            context.Messages.Add(new SkillMessage(SkillMessageTarget.Victim, line, victim));
        }

        context.Messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You have been resurrected...", victim));
    }
}
