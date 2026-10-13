using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Save;

[Command("save")]
internal sealed class SaveCommandHandler : ICommandHandler
{
    private readonly CharacterSaveQueue _saveQueue;

    public SaveCommandHandler(CharacterSaveQueue saveQueue)
    {
        _saveQueue = saveQueue;
    }
    
    /// <summary>
    /// Queue the save and report when it's done. Commands hold the game state gate, and the save worker needs it
    /// to read the player, so waiting here would deadlock (#61).
    /// </summary>
    public ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.Session;
        var saved = _saveQueue.QueueSaveAndWaitAsync(context.CharacterId, context.Player, cancellationToken);

        _ = saved.ContinueWith(
            task =>
            {
                var message = task.IsCompletedSuccessfully
                    ? task.Result ? "Character saved." : "Error: Character not found."
                    : $"Error saving character: {task.Exception?.GetBaseException().Message}";
                return session.SendLineAsync(message, CancellationToken.None);
            },
            TaskScheduler.Default);

        return ValueTask.FromResult(CommandOutcome.Continue);
    }
}
