using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Save;

internal sealed class SaveCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Save;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SaveCommandHandler(
            serviceProvider.GetRequiredService<ICharacterRepository>(),
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
