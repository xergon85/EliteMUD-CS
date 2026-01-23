using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Wear;

internal sealed class WearCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WearCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<ActMessageService>(),
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
