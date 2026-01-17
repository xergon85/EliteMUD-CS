using EliteMud.Application.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal interface ICommandModule
{
    CommandKind Kind { get; }

    ICommandHandler CreateHandler(IServiceProvider serviceProvider);
}
