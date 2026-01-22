using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Consider;

internal sealed class ConsiderCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Consider;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        
        return new ConsiderCommandHandler(worldState);
    }
}
