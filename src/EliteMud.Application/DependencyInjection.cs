using EliteMud.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountQueryService, AccountQueryService>();

        return services;
    }
}
