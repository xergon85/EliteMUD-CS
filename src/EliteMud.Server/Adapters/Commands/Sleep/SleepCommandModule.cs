using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Sleep;

internal sealed class SleepCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            TargetPosition: Position.Sleeping,
            PlayerMessage: "You go to sleep.",
            RoomMessage: "{0} lies down and falls asleep.");

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
