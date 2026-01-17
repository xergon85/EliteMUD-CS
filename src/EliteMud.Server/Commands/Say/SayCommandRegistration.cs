using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Say;

internal sealed class SayCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.Say;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new SayCommandHandler(services);
    }
}
