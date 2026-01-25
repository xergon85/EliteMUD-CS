namespace EliteMud.Data.Entities;

public class CharacterEquipmentItem
{
    public int EquipmentId { get; set; }
    public int CharacterId { get; set; }
    public required string Slot { get; set; }
    public int ObjectDefinitionId { get; set; }
    
    /// <summary>
    /// JSON-serialized item data including container contents and state.
    /// Uses InventoryItemDto format for consistency with inventory.
    /// </summary>
    public string? ItemData { get; set; }

    // Navigation property
    public Character Character { get; set; } = null!;
}
