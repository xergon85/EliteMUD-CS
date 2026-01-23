using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.SetSkill;

internal sealed class SetSkillCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SetSkillCommandHandler(
            serviceProvider.GetRequiredService<CommandCatalog>());
    }
}
