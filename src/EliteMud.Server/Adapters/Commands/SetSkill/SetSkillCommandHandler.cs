using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.SetSkill;

[Command("setskill")]
internal sealed class SetSkillCommandHandler : ICommandHandler
{
    private readonly CommandCatalog _catalog;

    public SetSkillCommandHandler(CommandCatalog catalog)
    {
        _catalog = catalog;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // POC: Testing command - available to all players for validation
        // In production, this would require level 35+ (immortal/god level)
        // TODO: Re-enable level check after POC testing complete

        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            await context.Session.SendLineAsync("Usage: setskill <skill> <proficiency>", cancellationToken);
            await context.Session.SendLineAsync("Available skills: kick, bash, backstab (bs), dodge, parry, tumble, rescue", cancellationToken);
            await context.Session.SendLineAsync("Proficiency: 0-100", cancellationToken);
            return CommandOutcome.Continue;
        }

        var parts = command.Argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            await context.Session.SendLineAsync("Usage: setskill <skill> <proficiency>", cancellationToken);
            return CommandOutcome.Continue;
        }

        var skillName = parts[0].ToLowerInvariant();
        if (!TryParseSkill(skillName, out var skillType))
        {
            await context.Session.SendLineAsync($"Unknown skill: {skillName}", cancellationToken);
            await context.Session.SendLineAsync("Available skills: kick, bash, backstab (bs), dodge, parry, tumble, rescue", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (!byte.TryParse(parts[1], out var proficiency) || proficiency > 100)
        {
            await context.Session.SendLineAsync("Proficiency must be a number between 0 and 100.", cancellationToken);
            return CommandOutcome.Continue;
        }

        context.Player.SetSkill(skillType, proficiency);
        await context.Session.SendLineAsync($"You set your {skillName} skill to {proficiency}%.", cancellationToken);
        return CommandOutcome.Continue;
    }

    private static bool TryParseSkill(string skillName, out SkillType skillType)
    {
        return skillName switch
        {
            "kick" => Set(out skillType, SkillType.Kick),
            "bash" => Set(out skillType, SkillType.Bash),
            "backstab" or "bs" => Set(out skillType, SkillType.Backstab),
            "rescue" => Set(out skillType, SkillType.Rescue),
            "dodge" => Set(out skillType, SkillType.Dodge),
            "parry" => Set(out skillType, SkillType.Parry),
            "tumble" => Set(out skillType, SkillType.Tumble),
            _ => Unset(out skillType)
        };

        static bool Set(out SkillType skill, SkillType value)
        {
            skill = value;
            return true;
        }

        static bool Unset(out SkillType skill)
        {
            skill = default;
            return false;
        }
    }
}
