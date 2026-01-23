namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Factory for creating command handlers.
/// Handlers are decorated with [Command] attributes for routing.
/// </summary>
internal interface ICommandModule
{
    ICommandHandler CreateHandler(IServiceProvider serviceProvider);
}
