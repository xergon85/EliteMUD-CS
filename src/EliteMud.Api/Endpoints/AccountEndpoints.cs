using EliteMud.Application.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EliteMud.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/accounts").WithTags("Accounts");

        group.MapGet("/{accountId:int}/characters", GetCharactersAsync)
            .WithName("GetAccountCharacters");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<CharacterSummary>>, NotFound>> GetCharactersAsync(
        int accountId, IAccountQueryService accounts, CancellationToken ct)
    {
        var characters = await accounts.GetCharactersAsync(accountId, ct);

        return characters is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(characters);
    }
}
