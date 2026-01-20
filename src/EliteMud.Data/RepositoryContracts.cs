using System.Data.Common;
using EliteMud.Game;
using EliteMud.Data.Entities;

namespace EliteMud.Data;

public interface ISqliteConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public interface IWorldRepository
{
    ValueTask<WorldDefinition?> LoadWorldAsync(CancellationToken cancellationToken);
    ValueTask SaveWorldAsync(WorldDefinition world, CancellationToken cancellationToken);
}

public interface IScriptRepository
{
    ValueTask<IReadOnlyList<ScriptDefinition>> LoadScriptsAsync(CancellationToken cancellationToken);
    ValueTask SaveScriptAsync(ScriptDefinition script, CancellationToken cancellationToken);
}

public interface IAccountRepository
{
    Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(int accountId, CancellationToken cancellationToken = default);
    Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(int accountId, CancellationToken cancellationToken = default);
}

public interface ICharacterRepository
{
    Task<Character?> GetByIdAsync(int characterId, CancellationToken cancellationToken = default);
    Task<Character?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Character>> GetByAccountIdAsync(int accountId, CancellationToken cancellationToken = default);
    Task<Character> CreateAsync(Character character, CancellationToken cancellationToken = default);
    Task UpdateAsync(Character character, CancellationToken cancellationToken = default);
    Task DeleteAsync(int characterId, CancellationToken cancellationToken = default);
    Task<int> GetCharacterCountByAccountIdAsync(int accountId, CancellationToken cancellationToken = default);
}
