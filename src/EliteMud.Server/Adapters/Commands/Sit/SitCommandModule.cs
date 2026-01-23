using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Sit;

internal sealed class SitCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            TargetPosition: Position.Sitting,
            PlayerMessage: "You sit down.",
            RoomMessage: "{0} sits down.");

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
