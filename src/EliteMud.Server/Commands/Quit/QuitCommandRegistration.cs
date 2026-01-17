using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Quit;

internal sealed class QuitCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.Quit;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new QuitCommandHandler();
    }
}
