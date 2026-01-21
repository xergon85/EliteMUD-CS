using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wimpy;

internal sealed class WimpyCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Wimpy;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new WimpyCommandHandler();
    }
}
