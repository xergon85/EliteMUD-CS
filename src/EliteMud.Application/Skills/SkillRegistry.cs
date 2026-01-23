using System.Reflection;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Skills;

/// <summary>
/// Central registry for all skill handlers (active and passive).
/// Auto-discovers skill implementations via reflection and provides dependency-injected access.
/// 
/// Design:
/// - Active skills (ISkillHandler) are triggered by player commands (kick, bash, etc.)
/// - Passive skills (IPassiveSkillHandler) are triggered automatically (dodge, parry, etc.)
/// - Both are registered together for unified skill discovery and metadata access
/// 
/// Thread-safety: All dictionaries are built once in constructor and never modified.
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<SkillType, ISkillHandler> _activeSkills;
    private readonly Dictionary<SkillType, IPassiveSkillHandler> _passiveSkills;

    public SkillRegistry(SkillMetadataRegistry metadataRegistry, FormulaEvaluator formulaEvaluator)
    {
        _activeSkills = DiscoverActiveSkills(metadataRegistry, formulaEvaluator);
        _passiveSkills = DiscoverPassiveSkills(metadataRegistry, formulaEvaluator);
    }

    /// <summary>
    /// Get an active skill handler by type.
    /// Throws if skill is not registered.
    /// </summary>
    public ISkillHandler GetActiveSkill(SkillType skillType)
    {
        if (_activeSkills.TryGetValue(skillType, out var skill))
        {
            return skill;
        }

        throw new InvalidOperationException($"Active skill {skillType} is not registered");
    }

    /// <summary>
    /// Get a passive skill handler by type.
    /// Throws if skill is not registered.
    /// </summary>
    public IPassiveSkillHandler GetPassiveSkill(SkillType skillType)
    {
        if (_passiveSkills.TryGetValue(skillType, out var skill))
        {
            return skill;
        }

        throw new InvalidOperationException($"Passive skill {skillType} is not registered");
    }

    /// <summary>
    /// Try to get an active skill handler by type.
    /// Returns false if skill is not registered.
    /// </summary>
    public bool TryGetActiveSkill(SkillType skillType, out ISkillHandler? skill)
    {
        return _activeSkills.TryGetValue(skillType, out skill);
    }

    /// <summary>
    /// Try to get a passive skill handler by type.
    /// Returns false if skill is not registered.
    /// </summary>
    public bool TryGetPassiveSkill(SkillType skillType, out IPassiveSkillHandler? skill)
    {
        return _passiveSkills.TryGetValue(skillType, out skill);
    }

    /// <summary>
    /// Get all registered active skills.
    /// </summary>
    public IReadOnlyCollection<ISkillHandler> GetAllActiveSkills()
    {
        return _activeSkills.Values;
    }

    /// <summary>
    /// Get all registered passive skills.
    /// </summary>
    public IReadOnlyCollection<IPassiveSkillHandler> GetAllPassiveSkills()
    {
        return _passiveSkills.Values;
    }

    /// <summary>
    /// Auto-discover all ISkillHandler implementations via reflection.
    /// Searches EliteMud.Application assembly for concrete classes implementing ISkillHandler.
    /// </summary>
    private static Dictionary<SkillType, ISkillHandler> DiscoverActiveSkills(SkillMetadataRegistry metadataRegistry, FormulaEvaluator formulaEvaluator)
    {
        var skills = new Dictionary<SkillType, ISkillHandler>();
        var assembly = Assembly.GetExecutingAssembly(); // EliteMud.Application

        var skillTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISkillHandler).IsAssignableFrom(t));

        foreach (var type in skillTypes)
        {
            // Instantiate the skill with SkillMetadataRegistry and FormulaEvaluator parameters
            if (Activator.CreateInstance(type, metadataRegistry, formulaEvaluator) is ISkillHandler skill)
            {
                if (skills.TryGetValue(skill.SkillType, out var skill1))
                {
                    throw new InvalidOperationException(
                        $"Duplicate active skill registration for {skill.SkillType}: " +
                        $"{skill1.GetType().Name} and {type.Name}");
                }

                skills[skill.SkillType] = skill;
            }
        }

        return skills;
    }

    /// <summary>
    /// Auto-discover all IPassiveSkillHandler implementations via reflection.
    /// Searches EliteMud.Application assembly for concrete classes implementing IPassiveSkillHandler.
    /// </summary>
    private static Dictionary<SkillType, IPassiveSkillHandler> DiscoverPassiveSkills(SkillMetadataRegistry metadataRegistry, FormulaEvaluator formulaEvaluator)
    {
        var skills = new Dictionary<SkillType, IPassiveSkillHandler>();

        // Passive skills now live in EliteMud.Application (same as active skills for easier maintenance)
        var assembly = typeof(SkillRegistry).Assembly;

        var skillTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPassiveSkillHandler).IsAssignableFrom(t));

        foreach (var type in skillTypes)
        {
            // Instantiate the skill with SkillMetadataRegistry and FormulaEvaluator parameters
            if (Activator.CreateInstance(type, metadataRegistry, formulaEvaluator) is IPassiveSkillHandler skill)
            {
                if (skills.TryGetValue(skill.SkillType, out var skill1))
                {
                    throw new InvalidOperationException(
                        $"Duplicate passive skill registration for {skill.SkillType}: " +
                        $"{skill1.GetType().Name} and {type.Name}");
                }

                skills[skill.SkillType] = skill;
            }
        }

        return skills;
    }
}
