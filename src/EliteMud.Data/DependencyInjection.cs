using EliteMud.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EliteMudDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();

        return services;
    }
}
