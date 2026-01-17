using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.ResetZone;

internal sealed class ResetZoneCommandHandler : ICommandHandler
{
    private readonly TelnetCommandServices _services;

    public ResetZoneCommandHandler(TelnetCommandServices services)
    {
        _services = services;
    }

    public CommandKind Kind => CommandKind.ResetZone;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        await _services.ResetZoneAsync(context, command.Argument, cancellationToken);
        return CommandOutcome.Continue;
    }
}
