namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class CommandHandlerRegistry
{
    public IReadOnlyList<ICommandHandler> BuildHandlers(
        IReadOnlyList<ICommandModule> modules,
        IServiceProvider serviceProvider)
    {
        var handlers = new List<ICommandHandler>();
        foreach (var module in modules)
        {
            handlers.Add(module.CreateHandler(serviceProvider));
        }

        return handlers;
    }
}
