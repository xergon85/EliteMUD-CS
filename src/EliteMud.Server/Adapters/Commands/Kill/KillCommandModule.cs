using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Kill;

internal sealed class KillCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Kill;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        var actService = serviceProvider.GetRequiredService<ActMessageService>();
        var connectionRegistry = serviceProvider.GetRequiredService<ConnectionRegistry>();
        
        return new KillCommandHandler(worldState, actService, connectionRegistry);
    }
}
