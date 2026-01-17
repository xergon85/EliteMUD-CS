using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.NoOp;

internal sealed class NoOpCommandModule : ICommandModule
{
    public CommandKind Kind => CommandKind.None;

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new NoOpCommandHandler();
    }
}
