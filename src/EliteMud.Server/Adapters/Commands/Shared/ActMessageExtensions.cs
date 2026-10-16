using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Extension methods for ConnectionContext: replies to the player and equipment messages. Act messages to others go
/// through <see cref="ActMessenger"/>.
/// </summary>
internal static class ActMessageExtensions
{
    /// <summary>
    /// Handles validation result by sending error message to player and returning Continue outcome.
    /// If validation succeeds, returns null and the caller should proceed with command execution.
    /// </summary>
    /// <returns>CommandOutcome.Continue if validation failed, null if validation succeeded</returns>
    public static async Task<CommandOutcome?> HandleValidationAsync(
        this ConnectionContext context,
        ValidationResult validationResult,
        CancellationToken cancellationToken = default)
    {
        if (!validationResult.IsValid)
        {
            await context.Session.SendLineAsync(validationResult.ErrorMessage!, cancellationToken);
            return CommandOutcome.Continue;
        }

        return null;
    }

    /// <summary>
    /// Sends equipment action messages to player and room for a single object.
    /// Used by wield/hold/wear/remove commands for successful equipment changes.
    /// </summary>
    public static void SendEquipMessage(
        this ConnectionContext context,
        ActMessenger act,
        string playerMessage,
        string roomMessage,
        ObjectDefinition obj)
    {
        act.ToChar(context.Player, playerMessage, obj: obj);
        act.ToNotChar(context.Player, roomMessage, obj: obj);
    }

    /// <summary>
    /// Executes a script hook and sends all script outputs to the player's session.
    /// </summary>
    public static async Task ExecuteScriptHookAsync(
        this ConnectionContext context,
        IScriptEngine scriptEngine,
        ScriptHook hook,
        RoomDefinition room,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        var scriptContext = new ScriptContext(context.Player, room, text);
        await scriptEngine.ExecuteAsync(hook, scriptContext, cancellationToken);
        
        foreach (var output in scriptContext.Outputs)
        {
            await context.Session.SendLineAsync(output, cancellationToken);
        }
    }
}
