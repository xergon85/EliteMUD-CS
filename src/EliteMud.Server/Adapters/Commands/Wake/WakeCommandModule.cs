using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Wake;

internal sealed class WakeCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Wake;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WakeCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
