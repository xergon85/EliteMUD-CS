using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Skills;
using System.Reflection;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class CommandRouter
{
    private readonly Dictionary<CommandKind, ICommandHandler> _handlersByKind;
    private readonly Dictionary<string, ICommandHandler> _handlersByVerb;

    public CommandRouter(IEnumerable<ICommandHandler> handlers)
    {
        _handlersByKind = new Dictionary<CommandKind, ICommandHandler>();
        _handlersByVerb = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var handler in handlers)
        {
            // Legacy routing by CommandKind
            _handlersByKind[handler.Kind] = handler;
            
            // New routing by verb from [Command] attribute
            CommandAttribute? attribute = null;
            
            // Check if handler itself has [Command] attribute
            attribute = handler.GetType().GetCustomAttribute<CommandAttribute>();
            
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
            
            if (attribute != null)
            {
                // Register primary command name
                _handlersByVerb[attribute.Name] = handler;
                
                // Register all aliases
                foreach (var alias in attribute.Aliases)
                {
                    _handlersByVerb[alias] = handler;
                }
            }
        }
    }

    public ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Try verb-based routing first (attribute-based)
        if (!string.IsNullOrEmpty(command.Verb) && 
            _handlersByVerb.TryGetValue(command.Verb, out var handler))
        {
            return handler.HandleAsync(command, context, cancellationToken);
        }
        
        // Fall back to legacy CommandKind routing
        if (_handlersByKind.TryGetValue(command.Kind, out handler))
        {
            return handler.HandleAsync(command, context, cancellationToken);
        }

        return ValueTask.FromResult(CommandOutcome.Unknown);
    }
}
