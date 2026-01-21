namespace EliteMud.Data.Entities;

public class CharacterEquipmentItem
{
    public int EquipmentId { get; set; }
    public int CharacterId { get; set; }
    public required string Slot { get; set; }
    public int ObjectDefinitionId { get; set; }

    // Navigation property
    public Character Character { get; set; } = null!;
}
