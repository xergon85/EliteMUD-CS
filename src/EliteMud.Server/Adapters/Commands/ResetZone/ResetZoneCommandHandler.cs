using EliteMud.Application.Commands.ResetZone;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.ResetZone;

internal sealed class ResetZoneCommandHandler : ICommandHandler
{
    private readonly CommandCatalog _catalog;
    private readonly LookCommandHandler _lookHandler;
    private readonly ResetZoneHandler _resetZoneHandler;

    public ResetZoneCommandHandler(
        IWorldState worldState,
        CommandCatalog catalog,
        LookCommandHandler lookHandler)
    {
        _catalog = catalog;
        _lookHandler = lookHandler;
        _resetZoneHandler = new ResetZoneHandler(worldState);
    }

    public CommandKind Kind => CommandKind.ResetZone;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        int? zoneId = null;
        if (!string.IsNullOrWhiteSpace(command.Argument))
        {
            if (!int.TryParse(command.Argument, out var parsedId))
            {
                await context.Session.SendLineAsync(_catalog.GetResetUsage(), cancellationToken);
                return CommandOutcome.Continue;
            }

            zoneId = parsedId;
        }

        var result = _resetZoneHandler.Handle(context.Player, zoneId);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (result.Success)
        {
            await _lookHandler.HandleAsync(new CommandRequest(CommandKind.Look, null, null), context, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}
