using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Tell;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Tell;

[Command("tell")]
internal sealed class TellCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ICharacterOutput _output;
    private readonly TellHandler _tellHandler;

    public TellCommandHandler(IWorldState worldState, ICharacterOutput output)
    {
        _worldState = worldState;
        _output = output;
        _tellHandler = new TellHandler();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Parse "tell <name> <message>" format
        var argument = command.Argument ?? string.Empty;
        var parts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        
        var recipientName = parts.Length > 0 ? parts[0] : null;
        var message = parts.Length > 1 ? parts[1] : null;

        // Find the recipient by name (case-insensitive)
        var recipient = string.IsNullOrWhiteSpace(recipientName)
            ? null
            : _worldState.Population.Players
                .FirstOrDefault(p => p.Name.Equals(recipientName, StringComparison.OrdinalIgnoreCase));

        var result = _tellHandler.Handle(context.Player, recipient, recipientName, message);

        // Handle history request (legacy feature - not implemented yet)
        if (result.IsHistoryRequest)
        {
            await context.Session.SendLineAsync("Tell history not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Send message to sender
        await context.Session.SendLineAsync(result.Message, cancellationToken);

        if (!result.Success)
        {
            return CommandOutcome.Continue;
        }

        // Send message to recipient
        if (result.Recipient != null)
        {
            _output.SendLine(result.Recipient, result.RecipientMessage!);

            // Track last tell sender for reply command
            result.Recipient.LastTellSender = context.Player;
        }

        return CommandOutcome.Continue;
    }
}
