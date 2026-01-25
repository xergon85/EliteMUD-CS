using System.Text.Json;
using EliteMud.Game;

namespace EliteMud.Server;

internal static class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WorldDefinition? LoadWorld(string contentRoot)
    {
        var roomsPath = Path.Combine(contentRoot, "rooms", "rooms.json");
        if (!File.Exists(roomsPath))
        {
            return null;
        }

        RoomsFile? file;
        try
        {
            var json = File.ReadAllText(roomsPath);
            file = JsonSerializer.Deserialize<RoomsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load rooms: {exception.Message}");
            return null;
        }

        if (file?.Rooms is null || file.Rooms.Count == 0)
        {
            return null;
        }

        var rooms = new Dictionary<int, RoomDefinition>();
        foreach (var room in file.Rooms)
        {
            var exits = new List<ExitDefinition>();
            if (room.Exits is not null)
            {
                foreach (var exit in room.Exits)
                {
                    if (!Enum.TryParse<Direction>(exit.Direction ?? string.Empty, true, out var direction))
                    {
                        continue;
                    }

                    exits.Add(new ExitDefinition(
                        direction,
                        exit.TargetId,
                        exit.Description,
                        exit.Keywords,
                        exit.ExitFlags,
                        exit.KeyId));
                }
            }

            rooms[room.Id] = new RoomDefinition(
                room.Id, 
                room.Name ?? string.Empty, 
                room.Description ?? string.Empty, 
                exits,
                ParseRoomFlags(room.Flags),
                room.ZoneId);
        }

        return new WorldDefinition(rooms);
    }

    public static IReadOnlyList<ScriptDefinition> LoadScripts(string contentRoot)
    {
        var scriptsPath = Path.Combine(contentRoot, "scripts", "scripts.json");
        if (!File.Exists(scriptsPath))
        {
            return Array.Empty<ScriptDefinition>();
        }

        ScriptsFile? file;
        try
        {
            var json = File.ReadAllText(scriptsPath);
            file = JsonSerializer.Deserialize<ScriptsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load scripts: {exception.Message}");
            return Array.Empty<ScriptDefinition>();
        }

        if (file?.Scripts is null || file.Scripts.Count == 0)
        {
            return Array.Empty<ScriptDefinition>();
        }

        var scripts = new List<ScriptDefinition>();
        foreach (var script in file.Scripts)
        {
            if (string.IsNullOrWhiteSpace(script.Id) || string.IsNullOrWhiteSpace(script.Hook))
            {
                continue;
            }

            scripts.Add(new ScriptDefinition(script.Id, script.Hook, script.Body ?? string.Empty, script.When?.RoomId));
        }

        return scripts;
    }

    public static IReadOnlyList<MobDefinition> LoadMobs(string contentRoot)
    {
        var mobsPath = Path.Combine(contentRoot, "mobs", "mobs.json");
        if (!File.Exists(mobsPath))
        {
            return Array.Empty<MobDefinition>();
        }

        MobsFile? file;
        try
        {
            var json = File.ReadAllText(mobsPath);
            file = JsonSerializer.Deserialize<MobsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load mobs: {exception.Message}");
            return Array.Empty<MobDefinition>();
        }

        if (file?.Mobs is null || file.Mobs.Count == 0)
        {
            return Array.Empty<MobDefinition>();
        }

        var mobs = new List<MobDefinition>();
        foreach (var mob in file.Mobs)
        {
            var stats = mob.Stats ?? new StatContent();
            int armorClass = mob.Combat?.Armor ?? Math.Min(100, mob.Level * 10);
            int maxHitPoints = ParseHitDice(mob.Resources?.HitDice, mob.Level);
            
            // Parse attacks
            var attacks = new List<MobAttack>();
            if (mob.Attacks != null)
            {
                foreach (var attack in mob.Attacks)
                {
                    var (diceCount, diceSides, bonus) = ParseDiceString(attack.DamageDice);
                    attacks.Add(new MobAttack(
                        attack.Type ?? "Hit",
                        attack.DamageType,
                        attack.Chance,
                        diceCount,
                        diceSides,
                        bonus));
                }
            }
            
            // Parse combat stats
            MobCombat? combat = null;
            if (mob.Combat != null)
            {
                combat = new MobCombat(
                    mob.Combat.Hitroll,
                    mob.Combat.Damroll);
            }
            
            mobs.Add(new MobDefinition(
                mob.Id,
                mob.Name ?? string.Empty,
                mob.ShortDescription ?? string.Empty,
                mob.LongDescription ?? string.Empty,
                mob.Description ?? string.Empty,
                mob.Level,
                mob.Race ?? string.Empty,
                mob.Class ?? string.Empty,
                mob.Flags ?? new List<string>(),
                new StatBlock(
                    stats.Strength,
                    stats.Dexterity,
                    stats.Intelligence,
                    stats.Wisdom,
                    stats.Constitution,
                    stats.Charisma),
                mob.Resistances ?? new List<string>(),
                mob.Skills ?? new List<string>(),
                armorClass,
                maxHitPoints,
                mob.Alignment,
                attacks,
                combat));
        }

        return mobs;
    }

    public static IReadOnlyList<ObjectDefinition> LoadObjects(string contentRoot)
    {
        var objectsPath = Path.Combine(contentRoot, "objects", "objects.json");
        if (!File.Exists(objectsPath))
        {
            return Array.Empty<ObjectDefinition>();
        }

        ObjectsFile? file;
        try
        {
            var json = File.ReadAllText(objectsPath);
            file = JsonSerializer.Deserialize<ObjectsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load objects: {exception.Message}");
            return Array.Empty<ObjectDefinition>();
        }

        if (file?.Objects is null || file.Objects.Count == 0)
        {
            return Array.Empty<ObjectDefinition>();
        }

        var objects = new List<ObjectDefinition>();
        foreach (var obj in file.Objects)
        {
            // Use WearSlots if present, otherwise fallback to WearFlags (legacy JSON format)
            var wearSlots = obj.WearSlots ?? obj.WearFlags ?? new List<string>();
            var flags = obj.Flags ?? obj.ExtraFlags ?? new List<string>();
            
            // Parse affects from JSON
            var affects = new List<ObjectAffect>();
            if (obj.Affects != null)
            {
                foreach (var aff in obj.Affects)
                {
                    if (aff.Location != null && Enum.TryParse<AffectLocation>(aff.Location, true, out var location))
                    {
                        affects.Add(new ObjectAffect(location, aff.Modifier));
                    }
                }
            }
            
            objects.Add(new ObjectDefinition(
                obj.Id,
                obj.Name ?? string.Empty,
                obj.ShortDescription ?? string.Empty,
                obj.LongDescription ?? string.Empty,
                obj.Description ?? string.Empty,
                obj.Type ?? string.Empty,
                wearSlots,
                flags,
                obj.Details,
                obj.Values ?? new List<int>(),
                obj.Weight,
                obj.Cost,
                affects));
        }

        return objects;
    }

    public static IReadOnlyList<ZoneDefinition> LoadZones(string contentRoot)
    {
        var zonesPath = Path.Combine(contentRoot, "zones", "zones.json");
        if (!File.Exists(zonesPath))
        {
            return Array.Empty<ZoneDefinition>();
        }

        ZonesFile? file;
        try
        {
            var json = File.ReadAllText(zonesPath);
            file = JsonSerializer.Deserialize<ZonesFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load zones: {exception.Message}");
            return Array.Empty<ZoneDefinition>();
        }

        if (file?.Zones is null || file.Zones.Count == 0)
        {
            return Array.Empty<ZoneDefinition>();
        }

        var zones = new List<ZoneDefinition>();
        foreach (var zone in file.Zones)
        {
            var resets = new List<ZoneResetDefinition>();
            if (zone.ResetCommands is not null)
            {
                foreach (var command in zone.ResetCommands)
                {
                    resets.Add(ConvertResetCommand(command));
                }
            }

            var roomRange = zone.RoomRange ?? new RoomRangeContent();
            zones.Add(new ZoneDefinition(
                zone.Id,
                zone.Name ?? string.Empty,
                new RoomRange(roomRange.Min, roomRange.Max),
                zone.ResetMode ?? string.Empty,
                resets));
        }

        return zones;
    }

    public static IReadOnlyDictionary<int, SkillMetadata> LoadSkills(string contentRoot)
    {
        var skillsPath = Path.Combine(contentRoot, "skills", "skills.json");
        if (!File.Exists(skillsPath))
        {
            Console.WriteLine($"Skills file not found: {skillsPath}");
            return new Dictionary<int, SkillMetadata>();
        }

        SkillsFile? file;
        try
        {
            var json = File.ReadAllText(skillsPath);
            file = JsonSerializer.Deserialize<SkillsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load skills: {exception.Message}");
            return new Dictionary<int, SkillMetadata>();
        }

        if (file?.Skills is null || file.Skills.Count == 0)
        {
            Console.WriteLine("Skills file is empty or invalid");
            return new Dictionary<int, SkillMetadata>();
        }

        var skills = new Dictionary<int, SkillMetadata>();
        foreach (var skill in file.Skills)
        {
            var classRestrictions = new Dictionary<string, ClassSkillRestriction>();
            if (skill.ClassRestrictions is not null)
            {
                foreach (var restriction in skill.ClassRestrictions)
                {
                    if (!string.IsNullOrWhiteSpace(restriction.Class))
                    {
                        classRestrictions[restriction.Class] = new ClassSkillRestriction(
                            restriction.MinLevel,
                            restriction.MaxProficiency,
                            restriction.Difficulty);
                    }
                }
            }

            SkillMechanics? mechanics = null;
            if (skill.Mechanics is not null)
            {
                var requirements = skill.Mechanics.Requirements?
                    .Select(r => new SkillRequirement
                    {
                        Type = r.Type ?? string.Empty,
                        Value = r.Value ?? string.Empty,
                        Message = r.Message ?? string.Empty,
                        Implemented = r.Implemented
                    })
                    .ToList();

                var effects = skill.Mechanics.Effects?
                    .Select(e => new SkillEffect
                    {
                        Type = e.Type ?? string.Empty,
                        Target = e.Target,
                        Effect = e.Effect,
                        Value = e.Value?.ToString(),
                        Description = e.Description
                    })
                    .ToList();

                mechanics = new SkillMechanics
                {
                    DamageFormula = skill.Mechanics.DamageFormula,
                    DamageMultiplierFormula = skill.Mechanics.DamageMultiplierFormula,
                    HitFormula = skill.Mechanics.HitFormula,
                    ActivationFormula = skill.Mechanics.ActivationFormula,
                    EffectFormula = skill.Mechanics.EffectFormula,
                    Requirements = requirements,
                    Effects = effects,
                    Note = skill.Mechanics.Note
                };
            }

            var metadata = new SkillMetadata(
                skill.Id,
                skill.Name ?? string.Empty,
                skill.Aliases ?? new List<string>(),
                skill.Description ?? string.Empty,
                skill.Type ?? string.Empty,
                skill.Category ?? string.Empty,
                skill.MinimumLevel,
                skill.WaitStateRounds,
                skill.SkillgainCooldown,
                classRestrictions,
                mechanics);

            skills[skill.Id] = metadata;
        }

        Console.WriteLine($"Loaded {skills.Count} skill definitions from {skillsPath}");
        return skills;
    }

    public static IReadOnlyDictionary<int, SpellMetadata> LoadSpells(string contentRoot)
    {
        var spellsPath = Path.Combine(contentRoot, "spells", "spells.json");
        if (!File.Exists(spellsPath))
        {
            Console.WriteLine($"Spells file not found: {spellsPath}");
            return new Dictionary<int, SpellMetadata>();
        }

        SpellsFile? file;
        try
        {
            var json = File.ReadAllText(spellsPath);
            file = JsonSerializer.Deserialize<SpellsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load spells: {exception.Message}");
            return new Dictionary<int, SpellMetadata>();
        }

        if (file?.Spells is null || file.Spells.Count == 0)
        {
            Console.WriteLine("Spells file is empty or invalid");
            return new Dictionary<int, SpellMetadata>();
        }

        var spells = new Dictionary<int, SpellMetadata>();
        foreach (var spell in file.Spells)
        {
            var classRestrictions = new Dictionary<string, ClassSpellRestriction>();
            if (spell.ClassRestrictions is not null)
            {
                foreach (var restriction in spell.ClassRestrictions)
                {
                    if (!string.IsNullOrWhiteSpace(restriction.Class))
                    {
                        classRestrictions[restriction.Class] = new ClassSpellRestriction(
                            restriction.MinLevel,
                            restriction.MaxProficiency,
                            restriction.Difficulty);
                    }
                }
            }

            SpellMechanics? mechanics = null;
            if (spell.Mechanics is not null)
            {
                mechanics = new SpellMechanics
                {
                    DamageFormula = spell.Mechanics.DamageFormula,
                    HealingFormula = spell.Mechanics.HealingFormula,
                    SuccessFormula = spell.Mechanics.SuccessFormula,
                    DurationFormula = spell.Mechanics.DurationFormula,
                    ArmorClassBonusFormula = spell.Mechanics.ArmorClassBonusFormula,
                    HitrollBonusFormula = spell.Mechanics.HitrollBonusFormula,
                    DamrollBonusFormula = spell.Mechanics.DamrollBonusFormula,
                    StrengthBonusFormula = spell.Mechanics.StrengthBonusFormula,
                    Note = spell.Mechanics.Note
                };
            }

            var metadata = new SpellMetadata(
                spell.Id,
                spell.Name ?? string.Empty,
                spell.Aliases ?? new List<string>(),
                spell.Description ?? string.Empty,
                spell.Type ?? string.Empty,
                spell.School ?? string.Empty,
                spell.MinimumLevel,
                spell.ManaCost,
                spell.CastTimeRounds,
                spell.WaitStateRounds,
                spell.TargetType ?? "Self",
                classRestrictions,
                mechanics);

            spells[spell.Id] = metadata;
        }

        Console.WriteLine($"Loaded {spells.Count} spell definitions from {spellsPath}");
        return spells;
    }

    public static (WorldDefinition? World, IReadOnlyList<MobDefinition> Mobs, IReadOnlyList<ObjectDefinition> Objects, IReadOnlyList<ZoneDefinition> Zones) LoadFromZoneFiles(string zonesDirectory)
    {
        if (!Directory.Exists(zonesDirectory))
        {
            Console.WriteLine($"Zone directory not found: {zonesDirectory}");
            return (null, Array.Empty<MobDefinition>(), Array.Empty<ObjectDefinition>(), Array.Empty<ZoneDefinition>());
        }

        var zoneFiles = Directory.GetFiles(zonesDirectory, "zone_*.json");
        if (zoneFiles.Length == 0)
        {
            Console.WriteLine($"No zone files found in: {zonesDirectory}");
            return (null, Array.Empty<MobDefinition>(), Array.Empty<ObjectDefinition>(), Array.Empty<ZoneDefinition>());
        }

        Console.WriteLine($"Loading {zoneFiles.Length} zone files...");

        var allRooms = new Dictionary<int, RoomDefinition>();
        var allMobs = new Dictionary<int, MobDefinition>();
        var allObjects = new Dictionary<int, ObjectDefinition>();
        var allZones = new List<ZoneDefinition>();

        foreach (var zoneFile in zoneFiles)
        {
            try
            {
                var json = File.ReadAllText(zoneFile);
                var zoneData = JsonSerializer.Deserialize<ZoneGroupedFile>(json, JsonOptions);

                if (zoneData is null)
                {
                    Console.WriteLine($"  Skipped (null): {Path.GetFileName(zoneFile)}");
                    continue;
                }

                // Load rooms
                if (zoneData.Rooms is not null)
                {
                    foreach (var room in zoneData.Rooms)
                    {
                        var exits = new List<ExitDefinition>();
                        if (room.Exits is not null)
                        {
                            foreach (var exit in room.Exits)
                            {
                                if (Enum.TryParse<Direction>(exit.Direction ?? string.Empty, true, out var direction))
                                {
                                    exits.Add(new ExitDefinition(
                                        direction,
                                        exit.TargetId,
                                        exit.Description,
                                        exit.Keywords,
                                        exit.ExitFlags,
                                        exit.KeyId));
                                }
                            }
                        }

                        allRooms[room.Id] = new RoomDefinition(
                            room.Id, 
                            room.Name ?? "", 
                            room.Description ?? "", 
                            exits,
                            ParseRoomFlags(room.Flags),
                            room.ZoneId);
                    }
                }

                // Load mobs
                if (zoneData.Mobs is not null)
                {
                    foreach (var mob in zoneData.Mobs)
                    {
                        var mobDef = ParseMobDefinition(mob);
                        if (mobDef is not null)
                        {
                            allMobs[mobDef.Id] = mobDef;
                        }
                    }
                }

                // Load objects
                if (zoneData.Objects is not null)
                {
                    foreach (var obj in zoneData.Objects)
                    {
                        var objectDef = ParseObjectDefinition(obj);
                        if (objectDef is not null)
                        {
                            allObjects[objectDef.Id] = objectDef;
                        }
                    }
                }

                // Load zone definition
                if (zoneData.Zone is not null)
                {
                    var resets = new List<ZoneResetDefinition>();
                    if (zoneData.Zone.ResetCommands is not null)
                    {
                        foreach (var command in zoneData.Zone.ResetCommands)
                        {
                            resets.Add(ConvertResetCommand(command));
                        }
                    }

                    var roomRange = zoneData.Zone.RoomRange ?? new RoomRangeContent();
                    var zoneDef = new ZoneDefinition(
                        zoneData.Zone.Id,
                        zoneData.Zone.Name ?? string.Empty,
                        new RoomRange(roomRange.Min, roomRange.Max),
                        zoneData.Zone.ResetMode ?? string.Empty,
                        resets);

                    allZones.Add(zoneDef);
                }

                Console.WriteLine($"  Loaded: {Path.GetFileName(zoneFile)} ({zoneData.Rooms?.Count ?? 0} rooms, {zoneData.Mobs?.Count ?? 0} mobs, {zoneData.Objects?.Count ?? 0} objects)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error loading {Path.GetFileName(zoneFile)}: {ex.Message}");
            }
        }

        // Also load standalone objects.json (for quest equipment and other non-zone objects)
        var contentRoot = Path.Combine(zonesDirectory, "..", "content");
        var objectsPath = Path.Combine(contentRoot, "objects", "objects.json");
        if (File.Exists(objectsPath))
        {
            try
            {
                var json = File.ReadAllText(objectsPath);
                var objectsFile = JsonSerializer.Deserialize<ObjectsFile>(json, JsonOptions);
                
                if (objectsFile?.Objects != null)
                {
                    int loadedCount = 0;
                    foreach (var obj in objectsFile.Objects)
                    {
                        var objectDef = ParseObjectDefinition(obj);
                        if (objectDef is not null)
                        {
                            // Only add if not already loaded from a zone file
                            if (!allObjects.ContainsKey(objectDef.Id))
                            {
                                allObjects[objectDef.Id] = objectDef;
                                loadedCount++;
                            }
                        }
                    }
                    Console.WriteLine($"Loaded {loadedCount} standalone objects from objects.json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading standalone objects.json: {ex.Message}");
            }
        }

        Console.WriteLine($"Total: {allRooms.Count} rooms, {allMobs.Count} mobs, {allObjects.Count} objects, {allZones.Count} zones");

        var world = allRooms.Count > 0 ? new WorldDefinition(allRooms) : null;
        return (world, allMobs.Values.ToList(), allObjects.Values.ToList(), allZones);
    }

    private static MobDefinition? ParseMobDefinition(MobContent mob)
    {
        if (string.IsNullOrWhiteSpace(mob.Name))
        {
            return null;
        }

        var stats = new StatBlock(
            mob.Stats?.Strength ?? 10,
            mob.Stats?.Dexterity ?? 10,
            mob.Stats?.Intelligence ?? 10,
            mob.Stats?.Wisdom ?? 10,
            mob.Stats?.Constitution ?? 10,
            mob.Stats?.Charisma ?? 10);

        // Parse AC from Combat.Armor (default to level * 10, max 100 if not specified)
        int armorClass = mob.Combat?.Armor ?? Math.Min(100, mob.Level * 10);

        // Parse MaxHP from Resources.HitDice (default to level * 10 if not specified)
        int maxHitPoints = ParseHitDice(mob.Resources?.HitDice, mob.Level);

        return new MobDefinition(
            mob.Id,
            mob.Name,
            mob.ShortDescription ?? string.Empty,
            mob.LongDescription ?? string.Empty,
            mob.Description ?? string.Empty,
            mob.Level,
            mob.Race ?? "Unknown",
            mob.Class ?? "Unknown",
            mob.Flags ?? new List<string>(),
            stats,
            mob.Resistances ?? new List<string>(),
            mob.Skills ?? new List<string>(),
            armorClass,
            maxHitPoints,
            mob.Alignment, // Alignment from JSON (defaults to 0 if not set)
            Array.Empty<MobAttack>(), // Bootstrap mobs have no attacks
            null); // Bootstrap mobs have no combat stats
    }

    /// <summary>
    /// Parse hit dice formula like "1d1+30000" or "5d8+25" to get max HP.
    /// Legacy: hit dice in the format "XdY+Z" where max = X*Y + Z
    /// If empty or invalid, defaults to level * 10.
    /// </summary>
    private static int ParseHitDice(string? hitDice, int level)
    {
        if (string.IsNullOrWhiteSpace(hitDice))
        {
            return level * 10;
        }

        try
        {
            // Format: "XdY+Z" or "XdY"
            var parts = hitDice.Split('d');
            if (parts.Length != 2)
            {
                return level * 10;
            }

            if (!int.TryParse(parts[0], out int numDice))
            {
                return level * 10;
            }

            var secondPart = parts[1];
            int bonus = 0;
            int diceSize;

            if (secondPart.Contains('+'))
            {
                var subParts = secondPart.Split('+');
                if (!int.TryParse(subParts[0], out diceSize) || !int.TryParse(subParts[1], out bonus))
                {
                    return level * 10;
                }
            }
            else if (secondPart.Contains('-'))
            {
                var subParts = secondPart.Split('-');
                if (!int.TryParse(subParts[0], out diceSize) || !int.TryParse(subParts[1], out int penalty))
                {
                    return level * 10;
                }
                bonus = -penalty;
            }
            else
            {
                if (!int.TryParse(secondPart, out diceSize))
                {
                    return level * 10;
                }
            }

            // Calculate max HP: numDice * diceSize + bonus
            return numDice * diceSize + bonus;
        }
        catch
        {
            return level * 10;
        }
    }

    /// <summary>
    /// Parse dice string format "XdY+Z" into components.
    /// Example: "4d30+10" returns (4, 30, 10)
    /// </summary>
    private static (int diceCount, int diceSides, int bonus) ParseDiceString(string? diceString)
    {
        if (string.IsNullOrWhiteSpace(diceString))
        {
            return (0, 0, 0);
        }

        try
        {
            // Format: "XdY+Z" or "XdY" or "XdY-Z"
            var parts = diceString.Split('d');
            if (parts.Length != 2)
            {
                return (0, 0, 0);
            }

            if (!int.TryParse(parts[0], out int numDice))
            {
                return (0, 0, 0);
            }

            var secondPart = parts[1];
            int bonus = 0;
            int diceSize;

            if (secondPart.Contains('+'))
            {
                var subParts = secondPart.Split('+');
                if (!int.TryParse(subParts[0], out diceSize) || !int.TryParse(subParts[1], out bonus))
                {
                    return (0, 0, 0);
                }
            }
            else if (secondPart.Contains('-'))
            {
                var subParts = secondPart.Split('-');
                if (!int.TryParse(subParts[0], out diceSize) || !int.TryParse(subParts[1], out int penalty))
                {
                    return (0, 0, 0);
                }
                bonus = -penalty;
            }
            else
            {
                if (!int.TryParse(secondPart, out diceSize))
                {
                    return (0, 0, 0);
                }
            }

            return (numDice, diceSize, bonus);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static ObjectDefinition? ParseObjectDefinition(ObjectContent obj)
    {
        if (string.IsNullOrWhiteSpace(obj.Name))
        {
            return null;
        }

        ObjectDetails? details = obj.Details; // Details are already deserialized from JSON

        // Convert object affects from content format to game format
        var affects = (obj.Affects ?? Enumerable.Empty<ObjectAffectContent>())
            .Select(a => new ObjectAffect(ParseAffectLocation(a.Location), a.Modifier))
            .ToList();

        // Legacy compatibility: If armor has ArmorClass in Details, automatically add it as an affect
        // In legacy EliteMUD, armor pieces had AC value that would apply when worn
        // Modern format uses Affects array, but old content has AC in Details.Armor.ArmorClass
        if (details?.Armor != null && details.Armor.ArmorClass != 0)
        {
            // Legacy EliteMUD applies slot-based multipliers to armor AC:
            // WEAR_BODY (chest): 3x, WEAR_HEAD (helm): 2x, WEAR_LEGS (pants): 2x, all others: 1x
            // Determine multiplier from WearFlags
            int multiplier = 1;
            var wearFlags = obj.WearFlags ?? new List<string>();
            
            if (wearFlags.Any(f => f.Equals("Body", StringComparison.OrdinalIgnoreCase)))
                multiplier = 3;
            else if (wearFlags.Any(f => f.Equals("Head", StringComparison.OrdinalIgnoreCase)))
                multiplier = 2;
            else if (wearFlags.Any(f => f.Equals("Legs", StringComparison.OrdinalIgnoreCase)))
                multiplier = 2;
            
            // Apply multiplier: armor with AC 3 on body gives -9 AC bonus (3 × 3)
            // Negate because lower AC is better in EliteMUD
            int acBonus = -(details.Armor.ArmorClass * multiplier);
            affects.Add(new ObjectAffect(AffectLocation.ArmorClass, acBonus));
        }

        // Legacy compatibility: Convert HitPoints from armor/weapon Details to MaxHit affect
        // In legacy EliteMUD, items could have HP/Mana/Move bonuses stored in Details
        if (details?.Armor != null && details.Armor.HitPoints != 0)
        {
            affects.Add(new ObjectAffect(AffectLocation.MaxHit, details.Armor.HitPoints));
        }
        
        if (details?.Weapon != null && details.Weapon.HitPoints != 0)
        {
            affects.Add(new ObjectAffect(AffectLocation.MaxHit, details.Weapon.HitPoints));
        }

        return new ObjectDefinition(
            obj.Id,
            obj.Name,
            obj.ShortDescription ?? string.Empty,
            obj.LongDescription ?? string.Empty,
            obj.Description ?? string.Empty,
            obj.Type ?? "Unknown",
            obj.WearFlags ?? new List<string>(),
            obj.ExtraFlags ?? new List<string>(),
            details,
            obj.Values ?? new List<int>(),
            obj.Weight,
            obj.Cost,
            affects);
    }

    private static ZoneResetDefinition ConvertResetCommand(ZoneResetContent cmd)
    {
        // If modern format (semantic fields), use them directly
        if (cmd.Type is not null)
        {
            return new ZoneResetDefinition(
                cmd.Type,
                cmd.ObjectId,
                cmd.MobId,
                cmd.RoomId,
                cmd.MaxExisting,
                cmd.SpawnChance,
                cmd.EquipSlot,
                cmd.ContainerId,
                cmd.DoorDirection,
                cmd.DoorState,
                cmd.IfFlag == 1);
        }

        // Legacy format: map Command + Args to semantic fields
        var command = cmd.Command ?? string.Empty;
        var ifFlag = cmd.IfFlag == 1;

        return command switch
        {
            "M" => new ZoneResetDefinition(
                "LoadMob",
                ObjectId: null,
                MobId: cmd.Arg1,
                RoomId: cmd.Arg3,
                MaxExisting: cmd.Arg2,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "O" => new ZoneResetDefinition(
                "LoadObject",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: cmd.Arg3,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "E" => new ZoneResetDefinition(
                "EquipMob",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: cmd.Arg3,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "G" => new ZoneResetDefinition(
                "GiveMob",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "P" => new ZoneResetDefinition(
                "PutObject",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: cmd.Arg3,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "D" => new ZoneResetDefinition(
                "DoorState",
                ObjectId: null,
                MobId: null,
                RoomId: cmd.Arg1,
                MaxExisting: null,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: cmd.Arg2,
                DoorState: cmd.Arg3,
                ifFlag),

            "R" => new ZoneResetDefinition(
                "RemoveObject",
                ObjectId: cmd.Arg2,
                MobId: null,
                RoomId: cmd.Arg1,
                MaxExisting: null,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            _ => new ZoneResetDefinition(
                command,
                null, null, null, null, null, null, null, null, null, ifFlag)
        };
    }

    private static string JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private static AffectLocation ParseAffectLocation(string? location)
    {
        return location?.ToLower() switch
        {
            "strength" or "str" => AffectLocation.Strength,
            "dexterity" or "dex" => AffectLocation.Dexterity,
            "intelligence" or "int" => AffectLocation.Intelligence,
            "wisdom" or "wis" => AffectLocation.Wisdom,
            "constitution" or "con" => AffectLocation.Constitution,
            "charisma" or "cha" => AffectLocation.Charisma,
            "armor" or "ac" => AffectLocation.Armor,  // Flat AC from Affects, no multiplier
            "armorclass" => AffectLocation.ArmorClass, // Reserved for Details conversion with multiplier
            "hitroll" => AffectLocation.Hitroll,
            "damroll" => AffectLocation.Damroll,
            "saving_physical" or "savingphysical" => AffectLocation.SavingPhysical,
            "saving_mental" or "savingmental" => AffectLocation.SavingMental,
            "saving_magic" or "savingmagic" => AffectLocation.SavingMagic,
            "saving_poison" or "savingpoison" => AffectLocation.SavingPoison,
            "magic_resistance" or "magicresistance" => AffectLocation.MagicResistance,
            "hit" or "hp" or "maxhit" => AffectLocation.MaxHit,
            "mana" or "maxmana" => AffectLocation.MaxMana,
            "move" or "movement" or "maxmovement" => AffectLocation.MaxMovement,
            _ => AffectLocation.None
        };
    }

    /// <summary>
    /// Parse room flags from JSON string array to RoomFlags enum.
    /// Example: ["Dark", "Indoors", "Lawful"] -> RoomFlags.Dark | RoomFlags.Indoors | RoomFlags.Lawful
    /// </summary>
    private static RoomFlags ParseRoomFlags(List<string>? flags)
    {
        if (flags == null || flags.Count == 0)
        {
            return RoomFlags.None;
        }

        RoomFlags result = RoomFlags.None;
        foreach (var flag in flags)
        {
            if (Enum.TryParse<RoomFlags>(flag, true, out var parsed))
            {
                result |= parsed;
            }
        }
        return result;
    }

    private sealed class RoomsFile
    {
        public List<RoomContent> Rooms { get; set; } = new();
    }

    private sealed class RoomContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<ExitContent>? Exits { get; set; }
        public int? ZoneId { get; set; }
        public List<string>? Flags { get; set; }
    }

    private sealed class ExitContent
    {
        public string? Direction { get; set; }
        public int TargetId { get; set; }
        public string? Description { get; set; }
        public List<string>? Keywords { get; set; }
        public List<string>? ExitFlags { get; set; }
        public int? KeyId { get; set; }
    }

    private sealed class ScriptsFile
    {
        public List<ScriptContent> Scripts { get; set; } = new();
    }

    private sealed class ScriptContent
    {
        public string? Id { get; set; }
        public string? Hook { get; set; }
        public string? Body { get; set; }
        public ScriptWhen? When { get; set; }
    }

    private sealed class ScriptWhen
    {
        public int? RoomId { get; set; }
    }

    private sealed class MobsFile
    {
        public List<MobContent> Mobs { get; set; } = new();
    }

    private sealed class MobContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Description { get; set; }
        public int Level { get; set; }
        public string? Race { get; set; }
        public string? Class { get; set; }
        public List<string>? Flags { get; set; }
        public StatContent? Stats { get; set; }
        public List<string>? Resistances { get; set; }
        public List<string>? Skills { get; set; }
        public ResourcesContent? Resources { get; set; }
        public int Alignment { get; set; }
        public CombatContent? Combat { get; set; }
        public List<AttackContent>? Attacks { get; set; }
    }
    
    private sealed class AttackContent
    {
        public string? Type { get; set; }
        public int DamageType { get; set; }
        public int Chance { get; set; }
        public string? DamageDice { get; set; }
    }
    
    private sealed class ResourcesContent
    {
        public string? HitDice { get; set; }
        public int Mana { get; set; }
        public int Move { get; set; }
    }
    
    private sealed class CombatContent
    {
        public int Armor { get; set; }
        public int Hitroll { get; set; }
        public int Damroll { get; set; }
    }

    private sealed class StatContent
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Constitution { get; set; }
        public int Charisma { get; set; }
    }

    private sealed class ObjectsFile
    {
        public List<ObjectContent> Objects { get; set; } = new();
    }

    private sealed class ObjectContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public List<string>? WearSlots { get; set; }
        public List<string>? WearFlags { get; set; } // Legacy format
        public List<string>? Flags { get; set; }
        public List<string>? ExtraFlags { get; set; } // Legacy format
        public List<int>? Values { get; set; }
        public ObjectDetails? Details { get; set; }
        public int Weight { get; set; }
        public int Cost { get; set; }
        public List<ObjectAffectContent>? Affects { get; set; }
    }

    private sealed class ObjectAffectContent
    {
        public string? Location { get; set; }
        public int Modifier { get; set; }
    }

    private sealed class ZonesFile
    {
        public List<ZoneContent> Zones { get; set; } = new();
    }

    private sealed class ZoneGroupedFile
    {
        public ZoneContent? Zone { get; set; }
        public List<RoomContent>? Rooms { get; set; }
        public List<MobContent>? Mobs { get; set; }
        public List<ObjectContent>? Objects { get; set; }
    }

    private sealed class ZoneContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public RoomRangeContent? RoomRange { get; set; }
        public string? ResetMode { get; set; }
        public List<ZoneResetContent>? ResetCommands { get; set; }
    }

    private sealed class RoomRangeContent
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    private sealed class ZoneResetContent
    {
        public string? Type { get; set; }
        public string? Command { get; set; }
        public int? IfFlag { get; set; }
        public int? Arg1 { get; set; }
        public int? Arg2 { get; set; }
        public int? Arg3 { get; set; }
        
        // Semantic fields (for modern JSON format)
        public int? MobId { get; set; }
        public int? ObjectId { get; set; }
        public int? RoomId { get; set; }
        public int? MaxExisting { get; set; }
        public int? SpawnChance { get; set; }
        public int? EquipSlot { get; set; }
        public int? ContainerId { get; set; }
        public int? DoorDirection { get; set; }
        public int? DoorState { get; set; }
    }

    private sealed class SkillsFile
    {
        public int Version { get; set; }
        public string? Description { get; set; }
        public List<SkillContent> Skills { get; set; } = new();
    }

    private sealed class SkillContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Aliases { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Category { get; set; }
        public int MinimumLevel { get; set; }
        public int WaitStateRounds { get; set; }
        public int SkillgainCooldown { get; set; }
        public List<ClassRestrictionContent>? ClassRestrictions { get; set; }
        public SkillMechanicsContent? Mechanics { get; set; }
    }

    private sealed class ClassRestrictionContent
    {
        public string? Class { get; set; }
        public int? MinLevel { get; set; }
        public int MaxProficiency { get; set; }
        public int Difficulty { get; set; }
    }

    private sealed class SkillMechanicsContent
    {
        public string? DamageFormula { get; set; }
        public string? DamageMultiplierFormula { get; set; }
        public string? HitFormula { get; set; }
        public string? ActivationFormula { get; set; }
        public string? EffectFormula { get; set; }
        public List<SkillRequirementContent>? Requirements { get; set; }
        public List<SkillEffectContent>? Effects { get; set; }
        public string? Note { get; set; }
    }

    private sealed class SkillRequirementContent
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
        public string? Message { get; set; }
        public bool Implemented { get; set; } = true;
    }

    private sealed class SkillEffectContent
    {
        public string? Type { get; set; }
        public string? Target { get; set; }
        public string? Effect { get; set; }
        public object? Value { get; set; } // Can be string or number
        public string? Description { get; set; }
    }

    private sealed class SpellsFile
    {
        public int Version { get; set; }
        public string? Description { get; set; }
        public List<SpellContent> Spells { get; set; } = new();
    }

    private sealed class SpellContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Aliases { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? School { get; set; }
        public int MinimumLevel { get; set; }
        public int ManaCost { get; set; }
        public int CastTimeRounds { get; set; }
        public int WaitStateRounds { get; set; }
        public string? TargetType { get; set; }
        public List<ClassRestrictionContent>? ClassRestrictions { get; set; }
        public SpellMechanicsContent? Mechanics { get; set; }
    }

    private sealed class SpellMechanicsContent
    {
        public string? DamageFormula { get; set; }
        public string? HealingFormula { get; set; }
        public string? SuccessFormula { get; set; }
        public string? DurationFormula { get; set; }
        public string? ArmorClassBonusFormula { get; set; }
        public string? HitrollBonusFormula { get; set; }
        public string? DamrollBonusFormula { get; set; }
        public string? StrengthBonusFormula { get; set; }
        public string? Note { get; set; }
    }
}
