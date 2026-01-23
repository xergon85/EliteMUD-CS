namespace EliteMud.Game;

/// <summary>
/// Registry for skill metadata loaded from content/skills/skills.json
/// Provides fast lookups by skill ID, name, or alias.
/// 
/// NOTE: Skill metadata is loaded once at server startup. To reload skill formulas
/// after editing skills.json, restart the server with: dotnet run --project src/EliteMud.Server
/// 
/// Hot reload (without restart) would require:
/// - Making this registry mutable with ReaderWriterLockSlim for thread safety
/// - Adding a Reload() method to atomically replace metadata and rebuild indexes
/// - Creating a new SkillRegistry instance (which uses reflection to instantiate skills)
/// - Implementing an admin command (e.g., /sreload) to trigger the reload
/// - Proper error handling to rollback if new JSON is invalid
/// </summary>
public sealed class SkillMetadataRegistry
{
    private readonly IReadOnlyDictionary<int, SkillMetadata> _byId;
    private readonly IReadOnlyDictionary<string, SkillMetadata> _byName;
    private readonly IReadOnlyDictionary<string, SkillMetadata> _byAlias;

    public SkillMetadataRegistry(IReadOnlyDictionary<int, SkillMetadata> skillsById)
    {
        _byId = skillsById;

        // Build name index (lowercase for case-insensitive lookup)
        var byName = new Dictionary<string, SkillMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skillsById.Values)
        {
            byName[skill.Name] = skill;
        }
        _byName = byName;

        // Build alias index (lowercase for case-insensitive lookup)
        var byAlias = new Dictionary<string, SkillMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skillsById.Values)
        {
            foreach (var alias in skill.Aliases)
            {
                byAlias[alias] = skill;
            }
        }
        _byAlias = byAlias;
    }

    /// <summary>
    /// Get skill metadata by SkillType ID (e.g., 323 for Kick)
    /// </summary>
    public SkillMetadata? GetById(int skillId)
    {
        return _byId.TryGetValue(skillId, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get skill metadata by SkillType enum
    /// </summary>
    public SkillMetadata? GetBySkillType(SkillType skillType)
    {
        return GetById((int)skillType);
    }

    /// <summary>
    /// Get skill metadata by name (case-insensitive)
    /// </summary>
    public SkillMetadata? GetByName(string name)
    {
        return _byName.TryGetValue(name, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get skill metadata by alias (case-insensitive)
    /// E.g., "bs" → backstab
    /// </summary>
    public SkillMetadata? GetByAlias(string alias)
    {
        return _byAlias.TryGetValue(alias, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get skill metadata by name or alias (case-insensitive)
    /// Checks name first, then aliases.
    /// </summary>
    public SkillMetadata? GetByNameOrAlias(string nameOrAlias)
    {
        return GetByName(nameOrAlias) ?? GetByAlias(nameOrAlias);
    }

    /// <summary>
    /// Try to get skill metadata by name or alias
    /// </summary>
    public bool TryGet(string nameOrAlias, out SkillMetadata? metadata)
    {
        metadata = GetByNameOrAlias(nameOrAlias);
        return metadata is not null;
    }

    /// <summary>
    /// Get all skill metadata
    /// </summary>
    public IEnumerable<SkillMetadata> GetAll()
    {
        return _byId.Values;
    }

    /// <summary>
    /// Get count of loaded skills
    /// </summary>
    public int Count => _byId.Count;

    /// <summary>
    /// Check if a skill exists by ID
    /// </summary>
    public bool HasSkill(int skillId)
    {
        return _byId.ContainsKey(skillId);
    }

    /// <summary>
    /// Check if a skill exists by SkillType
    /// </summary>
    public bool HasSkill(SkillType skillType)
    {
        return HasSkill((int)skillType);
    }
}
