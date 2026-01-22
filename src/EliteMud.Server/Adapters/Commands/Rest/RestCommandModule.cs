using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Rest;

internal sealed class RestCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Rest;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new RestCommandHandler(
            serviceProvider.GetRequiredService<ConnectionRegistry>());
    }
}
