using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.NoOp;
using EliteMud.Server.Adapters.Commands.Skills;
using System.Reflection;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class CommandRouter
{
    private readonly Dictionary<string, ICommandHandler> _handlersByVerb;
    private readonly HashSet<string> _adminVerbs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICommandHandler? _emptyCommandHandler;
    private readonly AdminPolicy _adminPolicy;

    public CommandRouter(IEnumerable<ICommandHandler> handlers, AdminPolicy adminPolicy)
    {
        _adminPolicy = adminPolicy;
        _handlersByVerb = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            // Special case: NoOpCommandHandler handles empty commands (user presses Enter)
            if (handler is NoOpCommandHandler)
            {
                _emptyCommandHandler = handler;
                continue;
            }

            var attribute =
                // Check if handler itself has [Command] attribute
                handler.GetType().GetCustomAttribute<CommandAttribute>();

            // If not, check if it's a SkillCommandHandler wrapping an executor
            if (attribute == null && handler is SkillCommandHandler skillHandler)
            {
                // Use reflection to get the executor and check its type for [Command] attribute
                var executorField = typeof(SkillCommandHandler)
                    .GetField("_executor", BindingFlags.NonPublic | BindingFlags.Instance);

                if (executorField != null)
                {
                    var executor = executorField.GetValue(skillHandler);
                    if (executor != null)
                    {
                        attribute = executor.GetType().GetCustomAttribute<CommandAttribute>();
                    }
                }
            }

            if (attribute == null) continue;
            // Register primary command name
            _handlersByVerb[attribute.Name] = handler;

            // Register all aliases
            foreach (var alias in attribute.Aliases)
            {
                _handlersByVerb[alias] = handler;
            }

            if (attribute.Admin)
            {
                _adminVerbs.Add(attribute.Name);
                _adminVerbs.UnionWith(attribute.Aliases);
            }
        }
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Handle empty command (user pressed Enter)
        if (string.IsNullOrEmpty(command.Verb))
        {
            return _emptyCommandHandler != null
                ? await _emptyCommandHandler.HandleAsync(command, context, cancellationToken)
                : CommandOutcome.Continue;
        }

        // Admin commands don't exist for other players: same reply as an unknown command
        if (_adminVerbs.Contains(command.Verb) && !_adminPolicy.IsAdmin(context))
        {
            return CommandOutcome.Unknown;
        }

        // Check if player is waiting (combat lag)
        // Only block action commands, not informational commands like 'look', 'score', etc.
        if (!context.Player.CanAct() && IsActionCommand(command.Verb))
        {
            await context.Session.SendLineAsync(
                "You must wait before you can do that.",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Handle normal commands
        if (_handlersByVerb.TryGetValue(command.Verb, out var handler))
        {
            return await handler.HandleAsync(command, context, cancellationToken);
        }

        return CommandOutcome.Unknown;
    }

    /// <summary>
    /// Determine if a command is an "action" command that should be blocked by WaitState.
    /// Informational commands (look, score, who, etc.) are not blocked.
    /// </summary>
    private static bool IsActionCommand(string verb)
    {
        // Allow informational commands even when waiting
        var allowedDuringWait = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "look", "l",
            "score", "sc",
            "inventory", "i", "inv",
            "equipment", "eq",
            "who",
            "skills",
            "affects", "aff",
            "examine", "exa", "ex",
            "consider", "con",
            "time",
            "weather",
            "help"
        };

        return !allowedDuringWait.Contains(verb);
    }
}
