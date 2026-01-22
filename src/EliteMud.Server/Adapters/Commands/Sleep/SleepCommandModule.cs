using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Sleep;

internal sealed class SleepCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Sleep;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new SleepCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
