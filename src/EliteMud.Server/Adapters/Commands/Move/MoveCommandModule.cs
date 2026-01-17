using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Move;

internal sealed class MoveCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Move;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new MoveCommandHandler(
            serviceProvider.GetRequiredService<IWorldState>(),
            serviceProvider.GetRequiredService<IScriptEngine>(),
            serviceProvider.GetRequiredService<LookCommandHandler>());
    }
}
