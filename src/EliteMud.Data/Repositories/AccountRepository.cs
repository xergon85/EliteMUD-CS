using Microsoft.EntityFrameworkCore;
using EliteMud.Data.Entities;

namespace EliteMud.Data.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly EliteMudDbContext _context;

    public AccountRepository(EliteMudDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .Include(a => a.Characters.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .Include(a => a.Characters.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
    }

    public async Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        account.CreatedAt = DateTime.UtcNow;
        account.IsActive = true;
        
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);
        
        return account;
    }

    public async Task UpdateLastLoginAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts.FindAsync([accountId], cancellationToken);
        if (account != null)
        {
            account.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
