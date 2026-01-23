using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.SetLevel;

internal sealed class SetLevelCommandHandler : ICommandHandler
{
    public CommandKind Kind => CommandKind.SetLevel;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument?.Trim();
        
        if (string.IsNullOrWhiteSpace(argument))
        {
            await context.Session.SendLineAsync("Usage: setlevel <level>", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (!int.TryParse(argument, out int level))
        {
            await context.Session.SendLineAsync("Invalid level. Must be a number.", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (level < 1 || level > 60)
        {
            await context.Session.SendLineAsync("Level must be between 1 and 60.", cancellationToken);
            return CommandOutcome.Continue;
        }

        context.Player.Level = (byte)level;
        await context.Session.SendLineAsync($"Level set to {level}.", cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
