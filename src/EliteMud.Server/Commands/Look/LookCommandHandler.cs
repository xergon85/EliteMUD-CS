using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Look;

internal sealed class LookCommandHandler : ICommandHandler
{
    private readonly TelnetCommandServices _services;

    public LookCommandHandler(TelnetCommandServices services)
    {
        _services = services;
    }

    public CommandKind Kind => CommandKind.Look;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        await _services.RenderRoomAsync(context, cancellationToken);
        return CommandOutcome.Continue;
    }
}
