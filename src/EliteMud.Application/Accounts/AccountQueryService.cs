using EliteMud.Data;

namespace EliteMud.Application.Accounts;

public class AccountQueryService(IAccountRepository accounts, ICharacterRepository characters) : IAccountQueryService
{
    public async Task<IReadOnlyList<CharacterSummary>?> GetCharactersAsync(int accountId,
        CancellationToken ct = default)
    {
        if (await accounts.GetByIdAsync(accountId, ct) is null) return null;

        var list = await characters.GetByAccountIdAsync(accountId, ct);

        return list.Select(c => new CharacterSummary(c.CharacterId, c.Name, c.Race, c.CharacterClass, c.Level))
            .ToArray();
    }
}
