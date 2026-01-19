using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Get;

internal sealed class GetCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Get;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new GetCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
