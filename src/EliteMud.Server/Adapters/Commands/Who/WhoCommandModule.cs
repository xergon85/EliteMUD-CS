using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Who;

internal sealed class WhoCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Who;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WhoCommandHandler(serviceProvider.GetRequiredService<ConnectionRegistry>().GetConnections);
    }
}
