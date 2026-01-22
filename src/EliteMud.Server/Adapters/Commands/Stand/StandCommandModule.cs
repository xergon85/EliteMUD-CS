using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Stand;

internal sealed class StandCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Stand;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new StandCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
