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
    
    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Queue the save and wait for completion
            var success = await _saveQueue.QueueSaveAndWaitAsync(
                context.CharacterId, 
                context.Player, 
                cancellationToken);
            
            if (success)
            {
                await context.Session.SendLineAsync("Character saved.", cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync("Error: Character not found.", cancellationToken);
            }
            
            return CommandOutcome.Continue;
        }
        catch (Exception ex)
        {
            await context.Session.SendLineAsync($"Error saving character: {ex.Message}", cancellationToken);
            return CommandOutcome.Continue;
        }
    }
}
