using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Move;

internal sealed class MoveCommandHandler : ICommandHandler
{
    private readonly TelnetCommandServices _services;

    public MoveCommandHandler(TelnetCommandServices services)
    {
        _services = services;
    }

    public CommandKind Kind => CommandKind.Move;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (!command.Direction.HasValue)
        {
            return CommandOutcome.Unknown;
        }

        await _services.MoveAsync(context, command.Direction.Value, cancellationToken);
        return CommandOutcome.Continue;
    }
}
