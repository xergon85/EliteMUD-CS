using EliteMud.Application.Commands.Shared;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Say;

internal sealed class SayCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Say;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SayCommandHandler(
            serviceProvider.GetRequiredService<IScriptEngine>(),
            serviceProvider.GetRequiredService<ConnectionRegistry>().GetConnections);
    }
}
