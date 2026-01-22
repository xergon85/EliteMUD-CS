using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Stand;

internal sealed class StandCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Stand;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            Kind: CommandKind.Stand,
            TargetPosition: Position.Standing,
            PlayerMessage: "You stand up.",
            RoomMessage: "{0} stands up.");

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
