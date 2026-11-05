namespace EliteMud.Application.Accounts;

public interface IAccountQueryService
{
    Task<IReadOnlyList<CharacterSummary>?> GetCharactersAsync(int accountId, CancellationToken ct = default);
}

public record CharacterSummary(int Id, string Name, string Race, string Class, int Level);
