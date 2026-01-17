using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Look;

internal sealed class LookCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.Look;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new LookCommandHandler(services);
    }
}
