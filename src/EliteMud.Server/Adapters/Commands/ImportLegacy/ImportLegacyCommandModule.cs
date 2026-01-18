using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.ImportLegacy;

internal sealed class ImportLegacyCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.ImportLegacy;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new ImportLegacyCommandHandler(
            serviceProvider.GetRequiredService<ImportLegacyHandler>(),
            serviceProvider.GetRequiredService<CommandCatalog>());
    }
}
