using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Flee;

internal sealed class FleeCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Flee;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        var connectionRegistry = serviceProvider.GetRequiredService<ConnectionRegistry>();
        
        return new FleeCommandHandler(worldState, connectionRegistry.GetConnections);
    }
}
