using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Flee;

internal sealed class FleeCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        var connectionRegistry = serviceProvider.GetRequiredService<ConnectionRegistry>();
        var lookHandler = serviceProvider.GetRequiredService<LookCommandHandler>();
        var fleeService = serviceProvider.GetRequiredService<FleeHandler>();
        
        return new FleeCommandHandler(worldState, connectionRegistry.GetConnections, lookHandler, fleeService);
    }
}
