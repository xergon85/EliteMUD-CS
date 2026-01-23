using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Skills;

internal sealed class SkillsCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Skills;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SkillsCommandHandler(
            serviceProvider.GetRequiredService<CommandCatalog>());
    }
}
