namespace EliteMud.Data.Entities;

public class Character
{
    public int CharacterId { get; set; }
    public int AccountId { get; set; }
    public required string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Sex { get; set; }
    public required string Race { get; set; }
    public required string CharacterClass { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;

    // Stats
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Charisma { get; set; }

    // Vitals
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
    public int Movement { get; set; }
    public int MaxMovement { get; set; }

    // Combat
    public int ArmorClass { get; set; }
    public int Hitroll { get; set; }
    public int Damroll { get; set; }
    public int Alignment { get; set; } = 0;
    public int WimpyLevel { get; set; } = 0;

    // Position & Regeneration
    public string Position { get; set; } = "Standing";  // Position enum as string for DB
    public int GainCount { get; set; } = 0;  // Accumulator for position-based regen

    // Skills - stored as JSON string: {"Kick":75,"Dodge":90}
    public string? Skills { get; set; }

    // Location & Resources
    public int RoomId { get; set; }
    public int Gold { get; set; } = 0;
    public int BankGold { get; set; } = 0;

    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPlayed { get; set; }
    public int PlayTimeMinutes { get; set; } = 0;
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public Account Account { get; set; } = null!;
    public ICollection<CharacterInventoryItem> Inventory { get; set; } = new List<CharacterInventoryItem>();
    public ICollection<CharacterEquipmentItem> Equipment { get; set; } = new List<CharacterEquipmentItem>();
}
