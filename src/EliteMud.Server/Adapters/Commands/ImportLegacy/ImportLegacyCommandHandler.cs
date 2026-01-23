using EliteMud.Application.Commands.ImportLegacy;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.ImportLegacy;

[Command("import-legacy")]
internal sealed class ImportLegacyCommandHandler : ICommandHandler
{
    private readonly ImportLegacyHandler _handler;
    private readonly CommandCatalog _catalog;

    public ImportLegacyCommandHandler(ImportLegacyHandler handler, CommandCatalog catalog)
    {
        _handler = handler;
        _catalog = catalog;
    }

    public CommandKind Kind => CommandKind.ImportLegacy;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            await context.Session.SendLineAsync(_catalog.GetImportLegacyUsage(), cancellationToken);
            return CommandOutcome.Continue;
        }

        var result = await _handler.HandleAsync(context.Player, command.Argument, cancellationToken);
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        return CommandOutcome.Continue;
    }
}
