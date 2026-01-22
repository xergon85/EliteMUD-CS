using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Sit;

internal sealed class SitCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Sit;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            Kind: CommandKind.Sit,
            TargetPosition: Position.Sitting,
            PlayerMessage: "You sit down.",
            RoomMessage: "{0} sits down.");

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
