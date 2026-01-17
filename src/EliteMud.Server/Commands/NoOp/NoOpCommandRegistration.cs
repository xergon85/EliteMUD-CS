using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.NoOp;

internal sealed class NoOpCommandRegistration : ICommandRegistration
{
    public CommandKind Kind => CommandKind.None;

    public ICommandHandler CreateHandler(TelnetCommandServices services)
    {
        return new NoOpCommandHandler();
    }
}
