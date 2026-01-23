using EliteMud.Application.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Command handler interface - now purely attribute-driven.
/// Use [Command("name", Aliases = [...])] on the implementation class.
/// </summary>
internal interface ICommandHandler
{
    ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken);
}
