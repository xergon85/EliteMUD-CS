using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Progression;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.SetLevel;

/// <summary>
/// Admin test command: jump to a mortal level. Going up runs the real level-up for every level in between; going
/// to the same or a lower level rebuilds the character from a new level 1 first (legacy advance).
/// </summary>
[Command("setlevel", Admin = true)]
internal sealed class SetLevelCommandHandler : ICommandHandler
{
    private readonly PlayerProgression _progression;

    public SetLevelCommandHandler(PlayerProgression progression)
    {
        _progression = progression;
    }

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

        if (level < 1 || level > ExperienceService.MaxMortalLevel)
        {
            await context.Session.SendLineAsync(
                $"Level must be between 1 and {ExperienceService.MaxMortalLevel}.", cancellationToken);
            return CommandOutcome.Continue;
        }

        var result = _progression.SetLevel(context.Player, level);
        var gains = $"+{result.HitPointGain} hp, +{result.ManaGain} mana, +{result.MovementGain} move.";
        var message = result switch
        {
            { Rebuilt: true, ToLevel: 1 } => "Rebuilt as a new level 1 character.",
            { Rebuilt: true } => $"Rebuilt as a new level 1 character, then raised to level {result.ToLevel}: {gains}",
            _ => $"Level {result.FromLevel} -> {result.ToLevel}: {gains}"
        };
        await context.Session.SendLineAsync(message, cancellationToken);
        
        return CommandOutcome.Continue;
    }
}
