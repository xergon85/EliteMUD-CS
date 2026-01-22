using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Sit;

internal sealed class SitCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Sit;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SitCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
