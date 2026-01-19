using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Wear;

internal sealed class WearCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Wear;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WearCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
