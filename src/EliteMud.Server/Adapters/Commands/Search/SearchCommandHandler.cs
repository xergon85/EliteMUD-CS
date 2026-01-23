using EliteMud.Application.Commands.Search;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Search;

[Command("search")]
internal sealed class SearchCommandHandler : ICommandHandler
{
    private readonly SearchHandler _searchHandler;

    public SearchCommandHandler(IWorldState worldState)
    {
        _searchHandler = new SearchHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _searchHandler.Handle(command.Argument ?? string.Empty);
        foreach (var line in result.Lines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }
        return CommandOutcome.Continue;
    }
}
