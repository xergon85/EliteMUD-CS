using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.ResetZone;

internal sealed class ResetZoneCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new ResetZoneCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<CommandCatalog>(),
            serviceProvider.GetRequiredService<LookCommandHandler>());
    }
}
