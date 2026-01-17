using EliteMud.Application;

namespace EliteMud.Server;

internal sealed class WhoCommandHandler : ICommandHandler
{
    private readonly TelnetCommandServices _services;

    public WhoCommandHandler(TelnetCommandServices services)
    {
        _services = services;
    }

    public CommandKind Kind => CommandKind.Who;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        await _services.ShowWhoAsync(context, cancellationToken);
        return CommandOutcome.Continue;
    }
}
