namespace EliteMud.Legacy.Import;

internal sealed class RoomsFile
{
    public RoomsFile(List<RoomContent> rooms)
    {
        this.rooms = rooms;
    }

    public List<RoomContent> rooms { get; }
}

internal sealed record RoomContent(
    int Id,
    string Name,
    string Description,
    int ZoneId,
    string Sector,
    IReadOnlyList<string> Flags,
    IReadOnlyList<ExitContent> Exits,
    IReadOnlyList<ExtraDescriptionContent> ExtraDescriptions,
    string? SpecialProc,
    IReadOnlyList<string> RoomPrograms,
    bool CrashRoom);

internal sealed record ExitContent(
    string Direction,
    int TargetId,
    string Description,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> ExitFlags,
    int? KeyId);

internal sealed record ExtraDescriptionContent(IReadOnlyList<string> Keywords, string Description);

internal sealed class ZonesFile
{
    public ZonesFile(List<ZoneContent> zones)
    {
        this.zones = zones;
    }

    public List<ZoneContent> zones { get; }
}

internal sealed record ZoneContent(
    int Id,
    string Name,
    int TopRoomId,
    int Lifespan,
    string ResetMode,
    IReadOnlyList<ZoneResetCommandContent> ResetCommands);

internal sealed record ZoneResetCommandContent(
    string Command,
    int IfFlag,
    int Arg1,
    int Arg2,
    int? Arg3,
    string? Comment);

internal sealed class MobsFile
{
    public MobsFile(List<MobContent> mobs)
    {
        this.mobs = mobs;
    }

    public List<MobContent> mobs { get; }
}

internal sealed class MobContent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Race { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public IReadOnlyList<string> Flags { get; set; } = new List<string>();
    public IReadOnlyList<string> Affects { get; set; } = new List<string>();
    public int Alignment { get; set; }
    public StatContent Stats { get; set; } = new(10, 10, 10, 10, 10, 10);
    public MobResourceContent Resources { get; set; } = new("", 0, 0);
    public MobCombatContent Combat { get; set; } = new(0, 0, 0);
    public List<MobAttackContent> Attacks { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public List<string> Resistances { get; set; } = new();
    public int Gold { get; set; }
    public int Experience { get; set; }
    public string DefaultPosition { get; set; } = "Standing";
    public string Sex { get; set; } = "Neutral";
    public string? ActionScript { get; set; }
    public string? SpecialProc { get; set; }
    public List<string> Programs { get; set; } = new();
}

internal sealed record StatContent(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Wisdom,
    int Constitution,
    int Charisma);

internal sealed record MobResourceContent(string HitDice, int Mana, int Move);

internal sealed record MobCombatContent(int Armor, int Hitroll, int Damroll);

internal sealed record MobAttackContent(
    string Type,
    int DamageType,
    int Chance,
    string DamageDice);

internal sealed class ObjectsFile
{
    public ObjectsFile(List<ObjectContent> objects)
    {
        this.objects = objects;
    }

    public List<ObjectContent> objects { get; }
}

internal sealed record ObjectContent(
    int Id,
    string Name,
    string ShortDescription,
    string Description,
    string ActionDescription,
    string Type,
    int Level,
    string AntiClass,
    IReadOnlyList<string> ExtraFlags,
    IReadOnlyList<string> WearFlags,
    IReadOnlyList<int> Values,
    int Weight,
    int Cost,
    int CostPerDay,
    IReadOnlyList<ExtraDescriptionContent> ExtraDescriptions,
    IReadOnlyList<ObjectAffectContent> Affects,
    IReadOnlyList<string> Bitvectors,
    string? SpecialProc);

internal sealed record ObjectAffectContent(string Location, int Modifier);
