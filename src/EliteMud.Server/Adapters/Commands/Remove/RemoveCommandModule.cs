using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Remove;

internal sealed class RemoveCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Remove;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new RemoveCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<ActMessageService>(),
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
