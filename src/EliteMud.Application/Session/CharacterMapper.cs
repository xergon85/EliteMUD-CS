using EliteMud.Data.Entities;
using EliteMud.Game;

namespace EliteMud.Application.Session;

/// <summary>
/// Maps between Character database entities and PlayerState runtime models.
/// </summary>
public static class CharacterMapper
{
    /// <summary>
    /// Converts a Character entity from the database into a PlayerState for runtime use.
    /// </summary>
    /// <param name="character">The character entity from database</param>
    /// <param name="connectionId">The connection ID for this session</param>
    /// <returns>A PlayerState ready for gameplay</returns>
    public static PlayerState ToPlayerState(Character character, int connectionId)
    {
        ArgumentNullException.ThrowIfNull(character);

        // Convert Sex string to byte (0 = neutral, 1 = male, 2 = female)
        byte sexValue = character.Sex?.ToLowerInvariant() switch
        {
            "male" => 1,
            "female" => 2,
            _ => 0
        };

        // Create PlayerState with mapped values
        var player = new PlayerState(
            id: connectionId,
            name: character.Name,
            roomId: character.RoomId,
            level: (byte)character.Level,
            characterClass: character.CharacterClass,
            race: character.Race,
            sex: sexValue
        )
        {
            Title = character.Title,
            Description = character.Description,
            
            // Stats
            Strength = (sbyte)character.Strength,
            Intelligence = (sbyte)character.Intelligence,
            Wisdom = (sbyte)character.Wisdom,
            Dexterity = (sbyte)character.Dexterity,
            Constitution = (sbyte)character.Constitution,
            Charisma = (sbyte)character.Charisma,
            
            // Vitals
            HitPoints = (short)character.HitPoints,
            MaxHitPoints = (short)character.MaxHitPoints,
            Mana = (short)character.Mana,
            MaxMana = (short)character.MaxMana,
            Movement = (short)character.Movement,
            MaxMovement = (short)character.MaxMovement,
            
            // Combat
            ArmorClass = (short)character.ArmorClass,
            Hitroll = (sbyte)character.Hitroll,
            Damroll = (sbyte)character.Damroll,
            Alignment = character.Alignment,
            WimpyLevel = (short)character.WimpyLevel,
            
            // Resources
            Gold = character.Gold,
            BankGold = character.BankGold,
            Experience = character.Experience
        };

        // Load inventory items
        foreach (var invItem in character.Inventory)
        {
            player.AddToInventory(invItem.ObjectId);
        }

        // Load equipment items
        // Note: Equipment slots in PlayerState use int slot IDs
        // We'll need a mapping from string slot names to int IDs
        // For now, skip equipment loading - will implement proper slot mapping later
        // foreach (var eqItem in character.Equipment)
        // {
        //     player.EquipToSlot(slotId, eqItem.ObjectId);
        // }

        return player;
    }

    /// <summary>
    /// Updates a Character entity with the current state from PlayerState.
    /// This is used for saving character state back to the database.
    /// </summary>
    /// <param name="character">The character entity to update</param>
    /// <param name="playerState">The current player state</param>
    public static void UpdateCharacterFromPlayerState(Character character, PlayerState playerState)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(playerState);

        // Don't update Name (immutable after creation)
        character.Title = playerState.Title;
        character.Description = playerState.Description;
        
        // Convert Sex byte back to string
        character.Sex = playerState.Sex switch
        {
            1 => "Male",
            2 => "Female",
            _ => "Neutral"
        };

        character.Race = playerState.Race;
        character.CharacterClass = playerState.CharacterClass;
        character.Level = playerState.Level;
        character.Experience = playerState.Experience;

        // Stats
        character.Strength = playerState.Strength;
        character.Intelligence = playerState.Intelligence;
        character.Wisdom = playerState.Wisdom;
        character.Dexterity = playerState.Dexterity;
        character.Constitution = playerState.Constitution;
        character.Charisma = playerState.Charisma;

        // Vitals
        character.HitPoints = playerState.HitPoints;
        character.MaxHitPoints = playerState.MaxHitPoints;
        character.Mana = playerState.Mana;
        character.MaxMana = playerState.MaxMana;
        character.Movement = playerState.Movement;
        character.MaxMovement = playerState.MaxMovement;

        // Combat
        character.ArmorClass = playerState.ArmorClass;
        character.Hitroll = playerState.Hitroll;
        character.Damroll = playerState.Damroll;
        character.Alignment = playerState.Alignment;
        character.WimpyLevel = playerState.WimpyLevel;

        // Location & Resources
        character.RoomId = playerState.RoomId;
        character.Gold = playerState.Gold;
        character.BankGold = playerState.BankGold;

        // Update last played
        character.LastPlayed = DateTime.UtcNow;

        // TODO: Update inventory and equipment
        // For now, we'll skip this since it requires more complex logic
        // to handle adds/removes/moves. Will implement in a future iteration.
    }
}
