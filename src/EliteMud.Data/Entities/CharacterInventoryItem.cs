namespace EliteMud.Data.Entities;

public class CharacterInventoryItem
{
    public int InventoryId { get; set; }
    public int CharacterId { get; set; }
    public int ObjectDefinitionId { get; set; }
    public int Quantity { get; set; } = 1;
    
    /// <summary>
    /// Points to the InventoryId of the container that holds this item.
    /// Null means this item is in the character's top-level inventory.
    /// </summary>
    public int? ContainerId { get; set; }
    
    /// <summary>
    /// JSON-serialized runtime object state (e.g., {"IsClosed": true, "IsLocked": false}).
    /// Only used for containers with closeable/lockable flags.
    /// </summary>
    public string? ObjectState { get; set; }
    
    /// <summary>
    /// Order of the item in its container (0 = oldest, higher = newer).
    /// Used to preserve item order when loading from database.
    /// </summary>
    public int SequenceOrder { get; set; }

    // Navigation properties
    public Character Character { get; set; } = null!;
    public CharacterInventoryItem? Container { get; set; }
    public ICollection<CharacterInventoryItem> Contents { get; set; } = new List<CharacterInventoryItem>();
}
