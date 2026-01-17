using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Who;

internal sealed class WhoCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.Who;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new WhoCommandHandler(services);
    }
}
