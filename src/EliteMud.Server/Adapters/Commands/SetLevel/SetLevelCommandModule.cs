using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.SetLevel;

internal sealed class SetLevelCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SetLevelCommandHandler();
    }
}
