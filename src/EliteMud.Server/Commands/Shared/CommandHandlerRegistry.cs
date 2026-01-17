using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Commands.Look;
using EliteMud.Server.Commands.Move;
using EliteMud.Server.Commands.NoOp;
using EliteMud.Server.Commands.Quit;
using EliteMud.Server.Commands.ResetZone;
using EliteMud.Server.Commands.Say;
using EliteMud.Server.Commands.Who;

namespace EliteMud.Server.Commands.Shared;

internal sealed class CommandHandlerRegistry
{
    private readonly CommandRegistry _registry;

    public CommandHandlerRegistry(CommandRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<ICommandHandler> BuildHandlers(TelnetCommandServices services)
    {
        var handlers = new List<ICommandHandler>();
        foreach (var kind in _registry.Commands)
        {
            handlers.Add(CreateHandler(kind, services));
        }

        return handlers;
    }

    private static ICommandHandler CreateHandler(CommandKind kind, TelnetCommandServices services)
    {
        return kind switch
        {
            CommandKind.None => new NoOpCommandHandler(),
            CommandKind.Quit => new QuitCommandHandler(),
            CommandKind.Look => new LookCommandHandler(services),
            CommandKind.Who => new WhoCommandHandler(services),
            CommandKind.ResetZone => new ResetZoneCommandHandler(services),
            CommandKind.Say => new SayCommandHandler(services),
            CommandKind.Move => new MoveCommandHandler(services),
            _ => new NoOpCommandHandler()
        };
    }
}
