using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Skills;

/// <summary>
/// Track skill - find the path to a target mob or player.
/// Legacy reference: graph.c perform_track(), act.informative.c do_track()
/// 
/// Mechanics:
/// - Uses PathfindingService to find shortest path to target
/// - Skill check determines success (random 1-101 vs skill proficiency)
/// - On success: shows direction to move toward target
/// - On failure: shows "You lose the trail"
/// - Max search distance scales with skill proficiency and player level
/// 
/// Metadata loaded from content/skills/skills.json
/// </summary>
public sealed class TrackSkill : ISkillHandler
{
    private readonly SkillMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public TrackSkill(SkillMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySkillType(SkillType.Track)
            ?? throw new InvalidOperationException("Track skill metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SkillType SkillType => SkillType.Track;
    
    public string Name => _metadata.Name;
    
    public string Description => _metadata.Description;
    
    public int MinimumLevel => _metadata.MinimumLevel;
    
    public int WaitStateRounds => _metadata.WaitStateRounds;

    public bool CanUse(ICombatant user)
    {
        // Must have skill proficiency
        if (user.GetSkill(SkillType) == 0)
            return false;

        // Must be conscious (standing, fighting, or resting - not sleeping/unconscious)
        if (user.Position < Position.Resting)
            return false;

        return true;
    }

    public string GetCannotUseMessage(ICombatant user)
    {
        if (user.GetSkill(SkillType) == 0)
            return "You don't know how to track.";

        if (user.Position < Position.Resting)
            return "You can't track from this position.";

        return "You can't track right now.";
    }

    /// <summary>
    /// Roll to see if tracking attempt succeeds using Lua formula from skills.json.
    /// Formula: "return random(1,101) <= skillPercent"
    /// Legacy: random(1, 101) vs skill_percent
    /// </summary>
    public bool RollSuccess(ICombatant tracker)
    {
        var hitFormula = _metadata.Mechanics?.HitFormula;
        if (string.IsNullOrEmpty(hitFormula))
        {
            // Fallback to legacy hardcoded logic
            var roll = Random.Shared.Next(1, 102);
            return roll <= tracker.GetSkill(SkillType.Track);
        }

        var context = new
        {
            skillPercent = tracker.GetSkill(SkillType.Track)
        };

        return _formulaEvaluator.EvaluateBool(hitFormula, context);
    }

    /// <summary>
    /// Calculate maximum search distance for tracking.
    /// Higher skill proficiency and level = longer search distance.
    /// Formula: min(50 + skillPercent/2 + level, 200)
    /// Range: 50-200 rooms
    /// </summary>
    public int CalculateMaxDistance(ICombatant tracker)
    {
        var skillPercent = tracker.GetSkill(SkillType.Track);
        var level = tracker.Level;
        
        // Base distance of 50, plus skill bonus, plus level bonus, capped at 200
        var distance = 50 + (skillPercent / 2) + level;
        return Math.Min(distance, 200);
    }
}
