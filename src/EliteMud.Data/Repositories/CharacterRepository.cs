using Microsoft.EntityFrameworkCore;
using EliteMud.Data.Entities;

namespace EliteMud.Data.Repositories;

public class CharacterRepository : ICharacterRepository
{
    private readonly EliteMudDbContext _context;

    public CharacterRepository(EliteMudDbContext context)
    {
        _context = context;
    }

    public async Task<Character?> GetByIdAsync(int characterId, CancellationToken cancellationToken = default)
    {
        return await _context.Characters
            .Include(c => c.Inventory)
            .Include(c => c.Equipment)
            .FirstOrDefaultAsync(c => c.CharacterId == characterId && !c.IsDeleted, cancellationToken);
    }

    public async Task<Character?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Characters
            .Include(c => c.Inventory)
            .Include(c => c.Equipment)
            .FirstOrDefaultAsync(c => c.Name == name && !c.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<Character>> GetByAccountIdAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Characters
            .Where(c => c.AccountId == accountId && !c.IsDeleted)
            .OrderByDescending(c => c.LastPlayed)
            .ToListAsync(cancellationToken);
    }

    public async Task<Character> CreateAsync(Character character, CancellationToken cancellationToken = default)
    {
        character.CreatedAt = DateTime.UtcNow;
        character.IsDeleted = false;
        
        _context.Characters.Add(character);
        await _context.SaveChangesAsync(cancellationToken);
        
        return character;
    }

    public async Task UpdateAsync(Character character, CancellationToken cancellationToken = default)
    {
        character.LastPlayed = DateTime.UtcNow;
        
        try
        {
            _context.Characters.Update(character);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Character was already saved by another process (auto-save)
            // Reload the entity and try again
            var entry = _context.Entry(character);
            await entry.ReloadAsync(cancellationToken);
            
            // Re-apply changes
            entry.CurrentValues.SetValues(character);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _context.Characters.FindAsync([characterId], cancellationToken);
        if (character != null)
        {
            // Soft delete
            character.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCharacterCountByAccountIdAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Characters
            .CountAsync(c => c.AccountId == accountId && !c.IsDeleted, cancellationToken);
    }
}
