namespace EliteMud.Legacy.Import;

public sealed record LegacyImportOptions(
    bool IncludeRooms = true,
    bool IncludeZones = true,
    bool IncludeMobs = true,
    bool IncludeObjects = true);
