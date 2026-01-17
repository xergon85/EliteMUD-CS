using EliteMud.Application;

namespace EliteMud.Server;

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
