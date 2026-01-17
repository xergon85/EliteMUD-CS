using EliteMud.Game;

namespace EliteMud.Legacy.Import;

[Obsolete("Legacy importer now writes JSON directly.")]
public sealed record LegacyImportResult(
    WorldDefinition World,
    IReadOnlyList<MobDefinition> Mobs,
    IReadOnlyList<ObjectDefinition> Objects,
    IReadOnlyList<ZoneDefinition> Zones);
