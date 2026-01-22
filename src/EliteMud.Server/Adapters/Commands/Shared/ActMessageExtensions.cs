using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Extension methods for ConnectionContext to support act() style messaging.
/// Provides easy broadcasting to rooms with substitution codes.
/// </summary>
internal static class ActMessageExtensions
{
    /// <summary>
    /// Send a formatted message to the actor (the player performing the action).
    /// </summary>
    public static async Task ActToCharAsync(
        this ConnectionContext context,
        ActMessageService actService,
        string message,
        PlayerState? victim = null,
        ObjectDefinition? obj = null,
        string? textArg = null,
        CancellationToken cancellationToken = default)
    {
        var formatted = actService.FormatMessage(
            message,
            viewer: context.Player,
            actor: context.Player,
            victim: victim,
            obj: obj,
            textArg: textArg);

        await context.Session.SendAsync(formatted, cancellationToken);
    }

    /// <summary>
    /// Send a formatted message to everyone in the room including the actor.
    /// </summary>
    public static async Task ActToRoomAsync(
        this ConnectionContext context,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        string message,
        PlayerState? victim = null,
        ObjectDefinition? obj = null,
        string? textArg = null,
        CancellationToken cancellationToken = default)
    {
        var roomId = context.Player.RoomId;
        var playersInRoom = connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == roomId)
            .ToList();

        foreach (var playerConnection in playersInRoom)
        {
            var formatted = actService.FormatMessage(
                message,
                viewer: playerConnection.Player,
                actor: context.Player,
                victim: victim,
                obj: obj,
                textArg: textArg);

            await playerConnection.Session.SendAsync(formatted, cancellationToken);
        }
    }

    /// <summary>
    /// Send a formatted message to everyone in the room EXCEPT the actor.
    /// </summary>
    public static async Task ActToNotCharAsync(
        this ConnectionContext context,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        string message,
        PlayerState? victim = null,
        ObjectDefinition? obj = null,
        string? textArg = null,
        CancellationToken cancellationToken = default)
    {
        var roomId = context.Player.RoomId;
        var playersInRoom = connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == roomId && c.Player.Id != context.Player.Id)
            .ToList();

        foreach (var playerConnection in playersInRoom)
        {
            var formatted = actService.FormatMessage(
                message,
                viewer: playerConnection.Player,
                actor: context.Player,
                victim: victim,
                obj: obj,
                textArg: textArg);

            await playerConnection.Session.SendAsync(formatted, cancellationToken);
        }
    }

    /// <summary>
    /// Send a formatted message to the victim (the target of the action).
    /// </summary>
    public static async Task ActToVictAsync(
        this ConnectionContext context,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        string message,
        PlayerState victim,
        ObjectDefinition? obj = null,
        string? textArg = null,
        CancellationToken cancellationToken = default)
    {
        var formatted = actService.FormatMessage(
            message,
            viewer: victim,
            actor: context.Player,
            victim: victim,
            obj: obj,
            textArg: textArg);

        // Find the victim's connection and send the message
        var victimConnection = connectionRegistry.GetConnections()
            .FirstOrDefault(c => c.Player.Id == victim.Id);

        if (victimConnection != null)
        {
            await victimConnection.Session.SendAsync(formatted, cancellationToken);
        }
    }

    /// <summary>
    /// Send a formatted message to everyone in the room EXCEPT the actor and victim.
    /// </summary>
    public static async Task ActToNotVictAsync(
        this ConnectionContext context,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        string message,
        PlayerState? victim = null,
        ObjectDefinition? obj = null,
        string? textArg = null,
        CancellationToken cancellationToken = default)
    {
        var roomId = context.Player.RoomId;
        var playersInRoom = connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == roomId 
                     && c.Player.Id != context.Player.Id 
                     && (victim == null || c.Player.Id != victim.Id))
            .ToList();

        foreach (var playerConnection in playersInRoom)
        {
            var formatted = actService.FormatMessage(
                message,
                viewer: playerConnection.Player,
                actor: context.Player,
                victim: victim,
                obj: obj,
                textArg: textArg);

            await playerConnection.Session.SendAsync(formatted, cancellationToken);
        }
    }

    /// <summary>
    /// Broadcast a simple text message to everyone in the room EXCEPT the actor.
    /// Useful for simple announcements that don't need act() substitution.
    /// </summary>
    public static async Task BroadcastToRoomAsync(
        this ConnectionContext context,
        ConnectionRegistry connectionRegistry,
        string message,
        CancellationToken cancellationToken = default)
    {
        var playersInRoom = connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == context.Player.RoomId && c.Id != context.Id);

        foreach (var observer in playersInRoom)
        {
            await observer.Session.SendLineAsync(message, cancellationToken);
        }
    }

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
    public static async Task SendEquipMessageAsync(
        this ConnectionContext context,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        string playerMessage,
        string roomMessage,
        ObjectDefinition obj,
        CancellationToken cancellationToken = default)
    {
        await context.ActToCharAsync(
            actService,
            playerMessage,
            obj: obj,
            cancellationToken: cancellationToken);

        await context.ActToNotCharAsync(
            actService,
            connectionRegistry,
            roomMessage,
            obj: obj,
            cancellationToken: cancellationToken);
    }
}
