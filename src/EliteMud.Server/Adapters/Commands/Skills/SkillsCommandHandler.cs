using System.Text;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Skills;

[Command("skills", Aliases = new[] { "skill" })]
internal sealed class SkillsCommandHandler : ICommandHandler
{
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument?.Trim().ToLowerInvariant();
        
        // Check if user wants info mode (skill i / skills i)
        bool showInfo = argument == "i" || argument == "info";
        
        if (showInfo)
        {
            // Show what skills the class will learn (by level)
            await ShowAvailableSkills(context, cancellationToken);
        }
        else
        {
            // Show current skills and proficiencies
            await ShowCurrentSkills(context, cancellationToken);
        }
        
        return CommandOutcome.Continue;
    }

    private async Task ShowCurrentSkills(ConnectionContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Your skills and proficiencies:");
        sb.AppendLine();

        // Display all known skills
        var skills = new[]
        {
            (SkillType.Kick, "Kick"),
            (SkillType.Bash, "Bash"),
            (SkillType.Dodge, "Dodge"),
            (SkillType.Parry, "Parry"),
            (SkillType.Tumble, "Tumble")
        };

        bool hasAnySkill = false;
        foreach (var (skillType, skillName) in skills)
        {
            var proficiency = context.Player.GetSkill(skillType);
            if (proficiency > 0)
            {
                hasAnySkill = true;
                sb.AppendLine($"  {skillName,-15} {proficiency,3}%");
            }
        }

        if (!hasAnySkill)
        {
            sb.AppendLine("  You haven't learned any skills yet.");
        }

        await context.Session.SendLineAsync(sb.ToString(), cancellationToken);
    }

    private async Task ShowAvailableSkills(ConnectionContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("These are the skills your class can learn:");
        sb.AppendLine();

        // POC: Hardcoded skill list with level requirements
        // In full implementation, this would come from JSON content files
        // based on player's class
        var availableSkills = new[]
        {
            (Level: 1, Name: "Kick", Description: "A powerful kick attack"),
            (Level: 1, Name: "Dodge", Description: "Avoid incoming attacks"),
            (Level: 5, Name: "Bash", Description: "Bash an opponent to the ground"),
            (Level: 10, Name: "Parry", Description: "Deflect attacks with your weapon"),
            (Level: 15, Name: "Tumble", Description: "Roll away from danger")
        };

        foreach (var skill in availableSkills.OrderBy(s => s.Level))
        {
            sb.AppendLine($"  Level {skill.Level,2}: {skill.Name,-15} - {skill.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("Note: In the POC, all skills are available to all classes.");
        sb.AppendLine("In the full implementation, this will be class-specific.");

        await context.Session.SendLineAsync(sb.ToString(), cancellationToken);
    }
}
