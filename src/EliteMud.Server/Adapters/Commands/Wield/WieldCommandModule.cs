using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Wield;

internal sealed class WieldCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Wield;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WieldCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<ActMessageService>(),
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
