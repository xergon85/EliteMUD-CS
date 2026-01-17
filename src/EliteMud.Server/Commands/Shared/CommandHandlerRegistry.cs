namespace EliteMud.Server.Commands.Shared;

internal sealed class CommandHandlerRegistry
{
    private readonly IReadOnlyList<ICommandRegistration> _registrations;

    public CommandHandlerRegistry(IReadOnlyList<ICommandRegistration> registrations)
    {
        _registrations = registrations;
    }

    public IReadOnlyList<ICommandHandler> BuildHandlers(TelnetCommandServices services)
    {
        var handlers = new List<ICommandHandler>();
        foreach (var registration in _registrations)
        {
            handlers.Add(registration.CreateHandler(services));
        }

        return handlers;
    }
}
