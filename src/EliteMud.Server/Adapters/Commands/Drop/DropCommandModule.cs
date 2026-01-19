using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Drop;

internal sealed class DropCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Drop;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new DropCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
