using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Quit;

internal sealed class QuitCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.Quit;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new QuitCommandHandler();
    }
}
