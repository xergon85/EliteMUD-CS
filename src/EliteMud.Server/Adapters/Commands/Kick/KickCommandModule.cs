using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Kick;

internal sealed class KickCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Kick;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        var actService = serviceProvider.GetRequiredService<ActMessageService>();
        var connectionRegistry = serviceProvider.GetRequiredService<ConnectionRegistry>();
        
        return new KickCommandHandler(worldState, actService, connectionRegistry);
    }
}
