using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Rest;

internal sealed class RestCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            TargetPosition: Position.Resting,
            PlayerMessage: "You sit down and rest.",
            RoomMessage: "{0} sits down and rests.");

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
