using EliteMud.Application.World;
using EliteMud.Data.Entities;
using EliteMud.Game;
using System.Text.Json;

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
    /// <param name="worldState">The world state to create object instances</param>
    /// <returns>A PlayerState ready for gameplay</returns>
    public static PlayerState ToPlayerState(Character character, int connectionId, IWorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(character);

        // Convert Sex string to byte (0 = neutral, 1 = male, 2 = female)
        byte sexValue = character.Sex?.ToLowerInvariant() switch
        {
            "male" => 1,
            "female" => 2,
            _ => 0
        };

        // Validate and fix room ID if needed
        // Legacy: interpreter.c:2071-2088 validates load room and falls back to start rooms
        int validatedRoomId = character.RoomId;
        if (validatedRoomId < 0 || !worldState.World.Rooms.ContainsKey(validatedRoomId))
        {
            // If saved room is invalid (NOWHERE/-1 or doesn't exist), use mortal start room
            // Legacy: mortal_start_room = 3001 (Temple of Midgaard)
            const int MortalStartRoom = 3001;
            validatedRoomId = MortalStartRoom;
        }

        // Create PlayerState with mapped values
        var player = new PlayerState(
            id: connectionId,
            name: character.Name,
            roomId: validatedRoomId,
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
            
            // Position & Regeneration
            Position = Enum.TryParse<Position>(character.Position, out var pos) ? pos : Position.Standing,
            GainCount = character.GainCount,
            
            // Resources
            Gold = character.Gold,
            BankGold = character.BankGold,
            Experience = character.Experience
        };

        // Load skills from JSON
        if (!string.IsNullOrWhiteSpace(character.Skills))
        {
            try
            {
                var skillDict = JsonSerializer.Deserialize<Dictionary<string, byte>>(character.Skills);
                if (skillDict != null)
                {
                    foreach (var (skillName, proficiency) in skillDict)
                    {
                        if (Enum.TryParse<SkillType>(skillName, out var skillType))
                        {
                            player.SetSkill(skillType, proficiency);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If skills JSON is invalid, just skip it
            }
        }
        
        // Load skillgain cooldown times from JSON
        if (!string.IsNullOrWhiteSpace(character.LastSkillgainTimes))
        {
            try
            {
                var skillgainDict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(character.LastSkillgainTimes);
                if (skillgainDict != null)
                {
                    foreach (var (skillName, timestamp) in skillgainDict)
                    {
                        if (Enum.TryParse<SkillType>(skillName, out var skillType))
                        {
                            player.SetSkillgainTime(skillType, timestamp);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON is invalid, just skip it (no cooldowns loaded)
            }
        }
        
        // Load spells from JSON
        if (!string.IsNullOrWhiteSpace(character.Spells))
        {
            try
            {
                var spellDict = JsonSerializer.Deserialize<Dictionary<string, byte>>(character.Spells);
                if (spellDict != null)
                {
                    foreach (var (spellName, proficiency) in spellDict)
                    {
                        if (Enum.TryParse<SpellType>(spellName, out var spellType))
                        {
                            player.SetSpell(spellType, proficiency);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If spells JSON is invalid, just skip it
            }
        }
        
        // Load spellgain cooldown times from JSON
        if (!string.IsNullOrWhiteSpace(character.LastSpellgainTimes))
        {
            try
            {
                var spellgainDict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(character.LastSpellgainTimes);
                if (spellgainDict != null)
                {
                    foreach (var (spellName, timestamp) in spellgainDict)
                    {
                        if (Enum.TryParse<SpellType>(spellName, out var spellType))
                        {
                            player.SetSpellgainTime(spellType, timestamp);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON is invalid, just skip it (no cooldowns loaded)
            }
        }
        
        // Load affects from JSON
        if (!string.IsNullOrWhiteSpace(character.Affects))
        {
            try
            {
                var affectsList = JsonSerializer.Deserialize<List<AffectDto>>(character.Affects);
                if (affectsList != null)
                {
                    foreach (var dto in affectsList)
                    {
                        // Only load affects that haven't expired yet
                        if (dto.DurationHours > 0)
                        {
                            var affect = new Affect
                            {
                                Type = dto.Type,
                                Location = dto.Location,
                                Modifier = dto.Modifier,
                                DurationHours = dto.DurationHours,
                                Source = dto.Source,
                                ToCharMessage = dto.ToCharMessage,
                                ToRoomMessage = dto.ToRoomMessage,
                                WearOffMessage = dto.WearOffMessage
                            };
                            player.AddAffect(affect);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If affects JSON is invalid, just skip it
            }
        }
        
        // Load inventory from JSON
        if (!string.IsNullOrWhiteSpace(character.InventoryJson))
        {
            try
            {
                var inventoryItems = JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryJson);
                if (inventoryItems != null)
                {
                    foreach (var itemDto in inventoryItems)
                    {
                        var objectInstance = LoadInventoryItemRecursive(itemDto, worldState);
                        if (objectInstance != null)
                        {
                            player.AddToInventory(objectInstance.InstanceId);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If inventory JSON is invalid, just skip it (empty inventory)
            }
        }

        // Load equipment items
        foreach (var eqItem in character.Equipment)
        {
            // Parse slot name back to enum, then to int
            if (Enum.TryParse<EquipmentSlot>(eqItem.Slot, out var slotEnum))
            {
                ObjectInstance? objectInstance = null;
                
                // Try to load from ItemData JSON first (includes container contents and state)
                if (!string.IsNullOrWhiteSpace(eqItem.ItemData))
                {
                    try
                    {
                        var itemDto = JsonSerializer.Deserialize<InventoryItemDto>(eqItem.ItemData);
                        if (itemDto != null)
                        {
                            objectInstance = LoadInventoryItemRecursive(itemDto, worldState);
                        }
                    }
                    catch (JsonException)
                    {
                        // If JSON is invalid, fall back to creating from definition
                    }
                }
                
                // Fallback: Create fresh instance from definition (for old data or JSON errors)
                if (objectInstance == null)
                {
                    objectInstance = worldState.CreateObjectInstance(eqItem.ObjectDefinitionId);
                }
                
                if (objectInstance != null)
                {
                    var slotId = (int)slotEnum;
                    player.EquipToSlot(slotId, objectInstance.InstanceId);
                }
            }
        }

        return player;
    }

    /// <summary>
    /// Updates a Character entity with the current state from PlayerState.
    /// This is used for saving character state back to the database.
    /// </summary>
    /// <param name="character">The character entity to update</param>
    /// <param name="playerState">The current player state</param>
    /// <param name="worldState">The world state to resolve object instances</param>
    public static void UpdateCharacterFromPlayerState(Character character, PlayerState playerState, IWorldState worldState)
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

        // Position & Regeneration
        character.Position = playerState.Position.ToString();
        character.GainCount = playerState.GainCount;

        // Skills - serialize to JSON
        var allSkills = playerState.GetAllSkills();
        if (allSkills.Count > 0)
        {
            var skillDict = allSkills.ToDictionary(
                kvp => kvp.Key.ToString(), 
                kvp => kvp.Value
            );
            character.Skills = JsonSerializer.Serialize(skillDict);
        }
        else
        {
            character.Skills = null;
        }
        
        // Skillgain cooldown times - serialize to JSON
        var allSkillgainTimes = playerState.GetAllSkillgainTimes();
        if (allSkillgainTimes.Count > 0)
        {
            var skillgainDict = allSkillgainTimes.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            );
            character.LastSkillgainTimes = JsonSerializer.Serialize(skillgainDict);
        }
        else
        {
            character.LastSkillgainTimes = null;
        }
        
        // Spells - serialize to JSON
        var allSpells = playerState.GetAllSpells();
        if (allSpells.Count > 0)
        {
            var spellDict = allSpells.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            );
            character.Spells = JsonSerializer.Serialize(spellDict);
        }
        else
        {
            character.Spells = null;
        }
        
        // Spellgain cooldown times - serialize to JSON
        var allSpellgainTimes = playerState.GetAllSpellgainTimes();
        if (allSpellgainTimes.Count > 0)
        {
            var spellgainDict = allSpellgainTimes.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            );
            character.LastSpellgainTimes = JsonSerializer.Serialize(spellgainDict);
        }
        else
        {
            character.LastSpellgainTimes = null;
        }
        
        // Affects - serialize to JSON
        var affects = playerState.Affects;
        if (affects.Count > 0)
        {
            var affectDtos = affects.Select(a => new AffectDto
            {
                Type = a.Type,
                Location = a.Location,
                Modifier = a.Modifier,
                DurationHours = a.DurationHours,
                Source = a.Source,
                ToCharMessage = a.ToCharMessage,
                ToRoomMessage = a.ToRoomMessage,
                WearOffMessage = a.WearOffMessage
            }).ToList();
            character.Affects = JsonSerializer.Serialize(affectDtos);
        }
        else
        {
            character.Affects = null;
        }

        // Location & Resources
        character.RoomId = playerState.RoomId;
        character.Gold = playerState.Gold;
        character.BankGold = playerState.BankGold;

        // Update last played
        character.LastPlayed = DateTime.UtcNow;

        // Update inventory - serialize to JSON
        var inventoryItems = new List<InventoryItemDto>();
        foreach (var objectInstanceId in playerState.InventoryObjectIds)
        {
            var objectInstance = worldState.GetObjectInstance(objectInstanceId);
            if (objectInstance != null)
            {
                inventoryItems.Add(SaveInventoryItemRecursive(objectInstance));
            }
        }
        character.InventoryJson = inventoryItems.Count > 0 
            ? JsonSerializer.Serialize(inventoryItems) 
            : null;

        // Update equipment
        // Clear existing equipment and rebuild from current state
        character.Equipment.Clear();
        foreach (var (slotId, objectInstanceId) in playerState.EquipmentSlotToObjectId)
        {
            var objectInstance = worldState.GetObjectInstance(objectInstanceId);
            if (objectInstance != null)
            {
                var slotEnum = (EquipmentSlot)slotId;
                
                // Serialize item data (including container contents and state)
                var itemDto = SaveInventoryItemRecursive(objectInstance);
                var itemData = JsonSerializer.Serialize(itemDto);
                
                character.Equipment.Add(new CharacterEquipmentItem
                {
                    CharacterId = character.CharacterId,
                    Slot = slotEnum.ToString(),
                    ObjectDefinitionId = objectInstance.Definition.Id,
                    ItemData = itemData
                });
            }
        }
    }

    /// <summary>
    /// Recursively loads an inventory item and its container contents from JSON.
    /// </summary>
    private static ObjectInstance? LoadInventoryItemRecursive(InventoryItemDto itemDto, IWorldState worldState)
    {
        var objectInstance = worldState.CreateObjectInstance(itemDto.ObjectDefinitionId);
        if (objectInstance == null) return null;
        
        // Restore state
        if (itemDto.State != null)
        {
            objectInstance.IsClosed = itemDto.State.IsClosed;
            objectInstance.IsLocked = itemDto.State.IsLocked;
        }
        
        // Recursively load contents
        foreach (var contentDto in itemDto.Contents)
        {
            var contentInstance = LoadInventoryItemRecursive(contentDto, worldState);
            if (contentInstance != null)
            {
                objectInstance.AddItem(contentInstance);
            }
        }
        
        return objectInstance;
    }

    /// <summary>
    /// Recursively saves an inventory item and its container contents to DTO for JSON serialization.
    /// </summary>
    private static InventoryItemDto SaveInventoryItemRecursive(ObjectInstance objectInstance)
    {
        ObjectStateDto? state = null;
        if (objectInstance.Definition.Details?.Container != null)
        {
            state = new ObjectStateDto
            {
                IsClosed = objectInstance.IsClosed,
                IsLocked = objectInstance.IsLocked
            };
        }
        
        var contents = new List<InventoryItemDto>();
        foreach (var contentItem in objectInstance.Contents)
        {
            contents.Add(SaveInventoryItemRecursive(contentItem));
        }
        
        return new InventoryItemDto
        {
            ObjectDefinitionId = objectInstance.Definition.Id,
            Quantity = 1,
            State = state,
            Contents = contents
        };
    }
}

/// <summary>
/// DTO for serializing/deserializing Affect to JSON.
/// Needed because Affect uses required init properties which don't serialize well.
/// </summary>
internal sealed record AffectDto
{
    public AffectType Type { get; init; }
    public AffectLocation Location { get; init; }
    public int Modifier { get; init; }
    public int DurationHours { get; init; }
    public string? Source { get; init; }
    public string? ToCharMessage { get; init; }
    public string? ToRoomMessage { get; init; }
    public string? WearOffMessage { get; init; }
}

/// <summary>
/// DTO for serializing/deserializing ObjectInstance runtime state to JSON.
/// Used for persisting container state (IsClosed, IsLocked).
/// </summary>
public sealed record ObjectStateDto
{
    public bool IsClosed { get; init; }
    public bool IsLocked { get; init; }
}

/// <summary>
/// DTO for serializing/deserializing inventory items to JSON.
/// Represents a tree structure with nested containers.
/// </summary>
public sealed record InventoryItemDto
{
    public int ObjectDefinitionId { get; init; }
    public int Quantity { get; init; } = 1;
    public ObjectStateDto? State { get; init; }
    public List<InventoryItemDto> Contents { get; init; } = new();
}
