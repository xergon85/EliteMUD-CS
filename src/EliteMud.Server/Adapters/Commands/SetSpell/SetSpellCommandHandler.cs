using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.SetSpell;

[Command("setspell")]
internal sealed class SetSpellCommandHandler : ICommandHandler
{
    private readonly SpellMetadataRegistry _metadataRegistry;

    public SetSpellCommandHandler(SpellMetadataRegistry metadataRegistry)
    {
        _metadataRegistry = metadataRegistry;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // POC: Testing command - available to all players for validation
        // In production, this would require level 35+ (immortal/god level)

        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            await context.Session.SendLineAsync("Usage: setspell <spell_name_or_id> <proficiency>", cancellationToken);
            await context.Session.SendLineAsync("Examples: setspell 'magic missile' 75  OR  setspell 1 75", cancellationToken);
            await context.Session.SendLineAsync("Available spells: magic missile (1), burning hands (7), armor (15), bless (16), lightning bolt (26), cure light wounds (28), cure serious wounds (29)", cancellationToken);
            await context.Session.SendLineAsync("Proficiency: 0-100", cancellationToken);
            return CommandOutcome.Continue;
        }

        var parts = command.Argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await context.Session.SendLineAsync("Usage: setspell <spell_name_or_id> <proficiency>", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Parse proficiency (last argument)
        if (!byte.TryParse(parts[^1], out var proficiency) || proficiency > 100)
        {
            await context.Session.SendLineAsync("Proficiency must be a number between 0 and 100.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Parse spell name or ID (everything except last argument)
        var spellIdentifier = string.Join(' ', parts[..^1]).Trim('\'');

        // Try parsing as ID first
        SpellMetadata? metadata = null;
        if (int.TryParse(spellIdentifier, out var spellId))
        {
            metadata = _metadataRegistry.GetById(spellId);
        }
        else
        {
            // Try name/alias lookup
            metadata = _metadataRegistry.GetByName(spellIdentifier) ?? _metadataRegistry.GetByAlias(spellIdentifier);
        }

        if (metadata == null)
        {
            await context.Session.SendLineAsync($"Unknown spell: {spellIdentifier}", cancellationToken);
            await context.Session.SendLineAsync("Available spells: magic missile (1), burning hands (7), armor (15), bless (16), lightning bolt (26), cure light wounds (28), cure serious wounds (29)", cancellationToken);
            return CommandOutcome.Continue;
        }

        var spellType = (SpellType)metadata.Id;
        context.Player.SetSpell(spellType, proficiency);
        await context.Session.SendLineAsync($"You set your '{metadata.Name}' spell to {proficiency}%.", cancellationToken);
        return CommandOutcome.Continue;
    }
}
