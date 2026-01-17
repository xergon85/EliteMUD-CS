using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Say;

internal sealed class SayCommandHandler : ICommandHandler
{
    private readonly TelnetCommandServices _services;

    public SayCommandHandler(TelnetCommandServices services)
    {
        _services = services;
    }

    public CommandKind Kind => CommandKind.Say;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        await _services.SayAsync(context, command.Argument ?? string.Empty, cancellationToken);
        return CommandOutcome.Continue;
    }
}
