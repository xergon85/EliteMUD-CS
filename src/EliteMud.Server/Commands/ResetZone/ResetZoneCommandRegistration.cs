using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.ResetZone;

internal sealed class ResetZoneCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.ResetZone;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new ResetZoneCommandHandler(services);
    }
}
