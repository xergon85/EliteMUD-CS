using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Equipment;

internal sealed class EquipmentCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Equipment;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new EquipmentCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>());
    }
}
