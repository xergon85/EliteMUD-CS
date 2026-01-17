using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Move;

internal sealed class MoveCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.Move;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new MoveCommandHandler(services);
    }
}
