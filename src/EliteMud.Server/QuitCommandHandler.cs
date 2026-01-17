using EliteMud.Application;

namespace EliteMud.Server;

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
