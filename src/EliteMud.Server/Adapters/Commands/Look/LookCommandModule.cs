using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Look;

internal sealed class LookCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Look;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new LookCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<IScriptEngine>());
    }
}
