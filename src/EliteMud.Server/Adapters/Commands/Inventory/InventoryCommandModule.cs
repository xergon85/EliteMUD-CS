using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Inventory;

internal sealed class InventoryCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Inventory;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new InventoryCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
