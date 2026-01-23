namespace EliteMud.Game;

/// <summary>
/// Registry for spell metadata loaded from content/spells/spells.json
/// Provides fast lookups by spell ID, name, or alias.
/// 
/// NOTE: Spell metadata is loaded once at server startup. To reload spell formulas
/// after editing spells.json, restart the server with: dotnet run --project src/EliteMud.Server
/// 
/// Hot reload (without restart) would require:
/// - Making this registry mutable with ReaderWriterLockSlim for thread safety
/// - Adding a Reload() method to atomically replace metadata and rebuild indexes
/// - Creating a new SpellRegistry instance (which uses reflection to instantiate spells)
/// - Implementing an admin command (e.g., /splreload) to trigger the reload
/// - Proper error handling to rollback if new JSON is invalid
/// </summary>
public sealed class SpellMetadataRegistry
{
    private readonly IReadOnlyDictionary<int, SpellMetadata> _byId;
    private readonly IReadOnlyDictionary<string, SpellMetadata> _byName;
    private readonly IReadOnlyDictionary<string, SpellMetadata> _byAlias;

    public SpellMetadataRegistry(IReadOnlyDictionary<int, SpellMetadata> spellsById)
    {
        _byId = spellsById;

        // Build name index (lowercase for case-insensitive lookup)
        var byName = new Dictionary<string, SpellMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in spellsById.Values)
        {
            byName[spell.Name] = spell;
        }
        _byName = byName;

        // Build alias index (lowercase for case-insensitive lookup)
        var byAlias = new Dictionary<string, SpellMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in spellsById.Values)
        {
            foreach (var alias in spell.Aliases)
            {
                byAlias[alias] = spell;
            }
        }
        _byAlias = byAlias;
    }

    /// <summary>
    /// Get spell metadata by SpellType ID (e.g., 1 for Magic Missile)
    /// </summary>
    public SpellMetadata? GetById(int spellId)
    {
        return _byId.TryGetValue(spellId, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get spell metadata by SpellType enum
    /// </summary>
    public SpellMetadata? GetBySpellType(SpellType spellType)
    {
        return GetById((int)spellType);
    }

    /// <summary>
    /// Get spell metadata by name (case-insensitive)
    /// </summary>
    public SpellMetadata? GetByName(string name)
    {
        return _byName.TryGetValue(name, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get spell metadata by alias (case-insensitive)
    /// E.g., "mm" → magic missile, "clw" → cure light wounds
    /// </summary>
    public SpellMetadata? GetByAlias(string alias)
    {
        return _byAlias.TryGetValue(alias, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get spell metadata by name or alias (case-insensitive)
    /// Checks name first, then aliases.
    /// </summary>
    public SpellMetadata? GetByNameOrAlias(string nameOrAlias)
    {
        return GetByName(nameOrAlias) ?? GetByAlias(nameOrAlias);
    }

    /// <summary>
    /// Try to get spell metadata by name or alias
    /// </summary>
    public bool TryGet(string nameOrAlias, out SpellMetadata? metadata)
    {
        metadata = GetByNameOrAlias(nameOrAlias);
        return metadata is not null;
    }

    /// <summary>
    /// Get all spell metadata
    /// </summary>
    public IEnumerable<SpellMetadata> GetAll()
    {
        return _byId.Values;
    }

    /// <summary>
    /// Get count of loaded spells
    /// </summary>
    public int Count => _byId.Count;

    /// <summary>
    /// Check if a spell exists by ID
    /// </summary>
    public bool HasSpell(int spellId)
    {
        return _byId.ContainsKey(spellId);
    }

    /// <summary>
    /// Check if a spell exists by SpellType
    /// </summary>
    public bool HasSpell(SpellType spellType)
    {
        return HasSpell((int)spellType);
    }
}
