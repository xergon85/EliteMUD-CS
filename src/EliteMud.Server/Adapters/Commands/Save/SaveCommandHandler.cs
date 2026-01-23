using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Save;

[Command("save")]
internal sealed class SaveCommandHandler : ICommandHandler
{
    private readonly ICharacterRepository _characterRepository;
    private readonly IWorldState _worldState;

    public SaveCommandHandler(ICharacterRepository characterRepository, IWorldState worldState)
    {
        _characterRepository = characterRepository;
        _worldState = worldState;
    }
    public async ValueTask<CommandOutcome> HandleAsync(CommandRequest request, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Get character from repository by character ID
            var character = await _characterRepository.GetByIdAsync(context.CharacterId, cancellationToken);
            
            if (character is null)
            {
                await context.Session.SendLineAsync("Error: Character not found.", cancellationToken);
                return CommandOutcome.Continue;
            }

            // Update character entity from player state
            CharacterMapper.UpdateCharacterFromPlayerState(character, context.Player, _worldState);
            
            // Save to database
            await _characterRepository.UpdateAsync(character, cancellationToken);
            
            // Send confirmation message
            await context.Session.SendLineAsync("Character saved.", cancellationToken);
            
            return CommandOutcome.Continue;
        }
        catch (Exception ex)
        {
            await context.Session.SendLineAsync($"Error saving character: {ex.Message}", cancellationToken);
            return CommandOutcome.Continue;
        }
    }
}
