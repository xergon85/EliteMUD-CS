namespace EliteMud.Data.Entities;

public class CharacterInventoryItem
{
    public int InventoryId { get; set; }
    public int CharacterId { get; set; }
    public int ObjectDefinitionId { get; set; }
    public int Quantity { get; set; } = 1;

    // Navigation property
    public Character Character { get; set; } = null!;
}
