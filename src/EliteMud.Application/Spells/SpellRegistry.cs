using System.Reflection;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Spells;

/// <summary>
/// Central registry for all spell handlers.
/// Auto-discovers spell implementations via reflection and provides dependency-injected access.
/// 
/// Design:
/// - Spells (ISpellHandler) are triggered by player commands (cast 'magic missile', etc.)
/// - All spells are registered via reflection for unified spell discovery and metadata access
/// 
/// Thread-safety: Dictionary is built once in constructor and never modified.
/// </summary>
public sealed class SpellRegistry
{
    private readonly Dictionary<SpellType, ISpellHandler> _spells;

    public SpellRegistry(SpellMetadataRegistry metadataRegistry, FormulaEvaluator formulaEvaluator)
    {
        _spells = DiscoverSpells(metadataRegistry, formulaEvaluator);
    }

    /// <summary>
    /// Get a spell handler by type.
    /// Throws if spell is not registered.
    /// </summary>
    public ISpellHandler GetSpell(SpellType spellType)
    {
        if (_spells.TryGetValue(spellType, out var spell))
        {
            return spell;
        }

        throw new InvalidOperationException($"Spell {spellType} is not registered");
    }

    /// <summary>
    /// Try to get a spell handler by type.
    /// Returns false if spell is not registered.
    /// </summary>
    public bool TryGetSpell(SpellType spellType, out ISpellHandler? spell)
    {
        return _spells.TryGetValue(spellType, out spell);
    }

    /// <summary>
    /// Get all registered spells.
    /// </summary>
    public IReadOnlyCollection<ISpellHandler> GetAllSpells()
    {
        return _spells.Values;
    }

    /// <summary>
    /// Auto-discover all ISpellHandler implementations via reflection.
    /// Searches EliteMud.Application assembly for concrete classes implementing ISpellHandler.
    /// </summary>
    private static Dictionary<SpellType, ISpellHandler> DiscoverSpells(SpellMetadataRegistry metadataRegistry, FormulaEvaluator formulaEvaluator)
    {
        var spells = new Dictionary<SpellType, ISpellHandler>();
        var assembly = Assembly.GetExecutingAssembly(); // EliteMud.Application

        var spellTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISpellHandler).IsAssignableFrom(t));

        foreach (var type in spellTypes)
        {
            // Instantiate the spell with SpellMetadataRegistry and FormulaEvaluator parameters
            if (Activator.CreateInstance(type, metadataRegistry, formulaEvaluator) is ISpellHandler spell)
            {
                if (spells.TryGetValue(spell.SpellType, out var existingSpell))
                {
                    throw new InvalidOperationException(
                        $"Duplicate spell registration for {spell.SpellType}: " +
                        $"{existingSpell.GetType().Name} and {type.Name}");
                }

                spells[spell.SpellType] = spell;
            }
        }

        return spells;
    }
}
