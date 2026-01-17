using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Quit;

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
