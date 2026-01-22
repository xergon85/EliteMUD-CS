using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Wake;

internal sealed class WakeCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Wake;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var config = new PositionChangeConfig(
            Kind: CommandKind.Wake,
            TargetPosition: Position.Sitting, // Wake up to sitting, not standing
            PlayerMessage: "You wake and sit up.",
            RoomMessage: "{0} awakens.",
            UseWakeValidation: true); // Wake has special validation

        return new PositionChangeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>(),
            config);
    }
}
