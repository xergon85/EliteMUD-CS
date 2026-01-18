using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Legacy.Import;

namespace EliteMud.Application.Commands.ImportLegacy;

public sealed class ImportLegacyHandler
{
    private readonly LegacyContentImporter _importer;

    public ImportLegacyHandler(LegacyContentImporter importer)
    {
        _importer = importer;
    }

    public async ValueTask<CommandResult> HandleAsync(
        PlayerState player,
        string legacyPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await _importer.ImportAsync(legacyPath, "content", cancellationToken);
            return CommandResult.Ok("Import completed successfully.");
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Import failed: {ex.Message}");
        }
    }
}
