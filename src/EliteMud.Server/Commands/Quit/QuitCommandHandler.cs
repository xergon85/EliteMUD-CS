using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Shared;

namespace EliteMud.Server.Commands.Quit;

internal sealed class QuitCommandHandler : ICommandHandler
{
    public CommandKind Kind => CommandKind.Quit;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        await context.Session.SendLineAsync("Goodbye!", cancellationToken);
        return CommandOutcome.Disconnect;
    }
}
