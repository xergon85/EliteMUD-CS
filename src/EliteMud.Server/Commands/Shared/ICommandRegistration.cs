using EliteMud.Application.Commands.Shared;

namespace EliteMud.Server.Commands.Shared;

internal interface ICommandRegistration
{
    CommandKind Kind { get; }

    ICommandHandler CreateHandler(TelnetCommandServices services);
}
