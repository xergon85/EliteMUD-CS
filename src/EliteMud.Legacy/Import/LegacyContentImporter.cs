using System.Text;
using System.Text.Json;
using EliteMud.Game;

namespace EliteMud.Legacy.Import;

public sealed class LegacyContentImporter
{
    private const int MaxIterations = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async ValueTask ImportAsync(
        string legacyWorldPath,
        string outputContentPath,
        CancellationToken cancellationToken,
        LegacyImportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(legacyWorldPath))
        {
            throw new ArgumentException("Legacy path is required.", nameof(legacyWorldPath));
        }

        if (string.IsNullOrWhiteSpace(outputContentPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputContentPath));
        }

        var importOptions = options ?? new LegacyImportOptions();
        var worldPath = ResolveWorldPath(legacyWorldPath);

        if (importOptions.IncludeRooms)
        {
            var rooms = LoadRooms(Path.Combine(worldPath, "wld"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "rooms"));
            await WriteAsync(Path.Combine(outputContentPath, "rooms", "rooms.json"), new RoomsFile(rooms), cancellationToken);
        }

        if (importOptions.IncludeZones)
        {
            var zones = LoadZones(Path.Combine(worldPath, "zon"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "zones"));
            await WriteAsync(Path.Combine(outputContentPath, "zones", "zones.json"), new ZonesFile(zones), cancellationToken);
        }

        if (importOptions.IncludeMobs)
        {
            var mobs = LoadMobs(Path.Combine(worldPath, "mob"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "mobs"));
            await WriteAsync(Path.Combine(outputContentPath, "mobs", "mobs.json"), new MobsFile(mobs), cancellationToken);
        }

        if (importOptions.IncludeObjects)
        {
            var objPath = Path.Combine(worldPath, "obj");
            if (!Directory.Exists(objPath))
            {
                objPath = Path.Combine(worldPath, "objects");
            }

            var objects = LoadObjects(objPath, cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "objects"));
            await WriteAsync(Path.Combine(outputContentPath, "objects", "objects.json"), new ObjectsFile(objects), cancellationToken);
        }
    }

    private static async ValueTask WriteAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
    }

    private static string ResolveWorldPath(string legacyWorldPath)
    {
        var worldPath = legacyWorldPath;
        if (Directory.Exists(Path.Combine(worldPath, "lib", "world")))
        {
            worldPath = Path.Combine(worldPath, "lib", "world");
        }

        if (!Directory.Exists(Path.Combine(worldPath, "wld")))
        {
            throw new DirectoryNotFoundException($"Legacy world directory not found under {legacyWorldPath}.");
        }

        return worldPath;
    }

    private static List<RoomContent> LoadRooms(string roomsPath, CancellationToken cancellationToken)
    {
        var rooms = new List<RoomContent>();
        foreach (var file in Directory.EnumerateFiles(roomsPath, "*.wld"))
        {
            using var reader = CreateReader(file);
            var parser = new LegacyParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > MaxIterations)
                {
                    throw new InvalidOperationException($"Room import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }


                if (token == "$")
                {
                    break;
                }

                if (!token.StartsWith('#'))
                {
                    continue;
                }

                var vnum = ParseInt(token[1..]);
                var name = parser.ReadTildeString();
                if (name.StartsWith('$'))
                {
                    break;
                }

                var description = parser.ReadTildeString();
                var zoneId = parser.ReadNumber();
                var roomFlags = parser.ReadNumber();
                var sector = parser.ReadNumber();

                var exits = new List<ExitContent>();
                var extras = new List<ExtraDescriptionContent>();
                var crashRoom = false;
                string? specialProc = null;
                var roomPrograms = new List<string>();
                var sectionIterations = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (sectionIterations++ > MaxIterations)
                    {
                        throw new InvalidOperationException($"Room section import exceeded safe iteration limit in {file}.");
                    }

                    var marker = parser.ReadToken();
                    if (marker is null)
                    {
                        break;
                    }

                    if (marker == "S")
                    {
                        break;
                    }

                    if (marker.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                    {
                        var dir = ParseInt(marker[1..]);
                        var exitDesc = parser.ReadTildeString();
                        var keywords = parser.ReadTildeString();
                        var exitFlags = parser.ReadNumber();
                        var keyId = parser.ReadNumber();
                        var toRoom = parser.ReadNumber();

                        exits.Add(new ExitContent(
                            DirectionFromIndex(dir),
                            toRoom,
                            exitDesc,
                            SplitKeywords(keywords),
                            ExitFlags(exitFlags),
                            keyId < 0 ? null : keyId));
                        continue;
                    }

                    if (marker == "E")
                    {
                        var keywords = parser.ReadTildeString();
                        var extraDesc = parser.ReadTildeString();
                        extras.Add(new ExtraDescriptionContent(SplitKeywords(keywords), extraDesc));
                        continue;
                    }

                    if (marker == "C")
                    {
                        crashRoom = true;
                        continue;
                    }

                    if (marker == "P")
                    {
                        specialProc = parser.ReadToken();
                        continue;
                    }

                    if (marker == ">")
                    {
                        roomPrograms.Add(parser.ReadProgramBlock());
                        continue;
                    }
                }

                rooms.Add(new RoomContent(
                    vnum,
                    name,
                    description,
                    zoneId,
                    SectorFromIndex(sector),
                    RoomFlags(roomFlags),
                    exits,
                    extras,
                    specialProc,
                    roomPrograms,
                    crashRoom));
            }
        }

        return rooms;
    }

    private static List<ZoneContent> LoadZones(string zonesPath, CancellationToken cancellationToken)
    {
        var zones = new List<ZoneContent>();
        foreach (var file in Directory.EnumerateFiles(zonesPath, "*.zon"))
        {
            using var reader = CreateReader(file);
            var parser = new LegacyParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > MaxIterations)
                {
                    throw new InvalidOperationException($"Zone import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }


                if (!token.StartsWith('#'))
                {
                    continue;
                }

                var zoneId = ParseInt(token[1..]);
                if (zoneId >= 99999)
                {
                    break;
                }

                var name = parser.ReadTildeString();
                var topRoom = parser.ReadNumber();
                var lifespan = parser.ReadNumber();
                var resetMode = parser.ReadNumber();

                var commands = new List<ZoneResetCommandContent>();
                var commandIterations = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (commandIterations++ > MaxIterations)
                    {
                        throw new InvalidOperationException($"Zone reset import exceeded safe iteration limit in {file}.");
                    }

                    var line = parser.ReadLineSkippingWhitespace();
                    if (line is null)
                    {
                        break;
                    }

                    if (line.StartsWith("S"))
                    {
                        break;
                    }

                    if (line.StartsWith("*"))
                    {
                        continue;
                    }

                    var pieces = SplitTokens(line);
                    if (pieces.Count < 4)
                    {
                        continue;
                    }

                    var command = pieces[0];
                    var ifFlag = ParseInt(pieces[1]);
                    var arg1 = ParseInt(pieces[2]);
                    var arg2 = ParseInt(pieces[3]);
                    int? arg3 = null;
                    if (pieces.Count > 4 && int.TryParse(pieces[4], out var parsedArg3))
                    {
                        arg3 = parsedArg3;
                    }

                    var comment = ExtractComment(line);
                    commands.Add(new ZoneResetCommandContent(command, ifFlag, arg1, arg2, arg3, comment));
                }

                zones.Add(new ZoneContent(zoneId, name, topRoom, lifespan, ResetMode(resetMode), commands));
            }
        }

        return zones;
    }

    private static List<MobContent> LoadMobs(string mobsPath, CancellationToken cancellationToken)
    {
        var mobs = new List<MobContent>();
        foreach (var file in Directory.EnumerateFiles(mobsPath, "*.mob"))
        {
            using var reader = CreateReader(file);
            var parser = new LegacyParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > MaxIterations)
                {
                    throw new InvalidOperationException($"Mob import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }

                if (token == "$")
                {
                    break;
                }

                if (!token.StartsWith('#'))
                {
                    continue;
                }

                var vnum = ParseInt(token[1..]);
                if (vnum >= 99999)
                {
                    break;
                }

                var name = parser.ReadTildeString();
                var shortDesc = parser.ReadTildeString();
                var longDesc = parser.ReadTildeString();
                var description = parser.ReadTildeString();

                var race = parser.ReadNumber();
                var mobClass = parser.ReadNumber();
                var flags = parser.ReadNumber();
                var affects = parser.ReadNumber();
                var alignment = parser.ReadNumber();

                var format = parser.ReadToken();
                if (format is null)
                {
                    break;
                }

                var mob = new MobContent
                {
                    Id = vnum,
                    Name = name,
                    ShortDescription = shortDesc,
                    LongDescription = longDesc,
                    Description = description,
                    Level = 1,
                    Race = MobRaceFromIndex(race),
                    Class = MobClassFromIndex(mobClass),
                    Flags = MobFlags(flags),
                    Affects = AffectFlags(affects),
                    Alignment = alignment,
                    Stats = new StatContent(10, 10, 10, 10, 10, 10),
                    Skills = new List<string>(),
                    Resistances = new List<string>(),
                    Attacks = new List<MobAttackContent>()
                };

                SkipMobRecord(parser);
                mobs.Add(mob);
            }
        }

        return mobs;
    }

    private static List<ObjectContent> LoadObjects(string objectsPath, CancellationToken cancellationToken)
    {
        var objects = new List<ObjectContent>();
        foreach (var file in Directory.EnumerateFiles(objectsPath, "*.obj"))
        {
            using var reader = CreateReader(file);
            var parser = new LegacyParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > MaxIterations)
                {
                    throw new InvalidOperationException($"Object import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }

                if (token == "$")
                {
                    break;
                }

                if (!token.StartsWith('#'))
                {
                    continue;
                }

                var vnum = ParseInt(token[1..]);
                if (vnum >= 99999)
                {
                    break;
                }

                var name = parser.ReadTildeString();
                var shortDesc = parser.ReadTildeString();
                var description = parser.ReadTildeString();
                var actionDesc = parser.ReadTildeString();

                var type = parser.ReadNumber();
                var level = parser.ReadNumber();
                var antiClass = parser.ReadNumber();
                var extraFlags = parser.ReadNumber();
                var wearFlags = parser.ReadNumber();
                var values = new List<int>();
                for (var i = 0; i < 6; i++)
                {
                    values.Add(parser.ReadNumber());
                }

                var weight = parser.ReadNumber();
                var cost = parser.ReadNumber();
                var costPerDay = parser.ReadNumber();

                SkipObjectRecord(parser);

                objects.Add(new ObjectContent(
                    vnum,
                    name,
                    shortDesc,
                    description,
                    actionDesc,
                    ItemTypeFromIndex(type),
                    level,
                    AntiClassFromIndex(antiClass),
                    ItemExtraFlags(extraFlags),
                    ItemWearFlags(wearFlags),
                    values,
                    weight,
                    cost,
                    costPerDay,
                    new List<ExtraDescriptionContent>(),
                    new List<ObjectAffectContent>(),
                    new List<string>(),
                    null));
            }
        }

        return objects;
    }

    private static StreamReader CreateReader(string path)
    {
        return new StreamReader(path, Encoding.Latin1);
    }

    private static int ParseInt(string token)
    {
        return int.TryParse(token, out var value) ? value : 0;
    }

    private static string ResetMode(int mode) => mode switch
    {
        0 => "ResetNever",
        1 => "ResetWhenEmpty",
        2 => "ResetAlways",
        _ => "ResetAlways"
    };

    private static string DirectionFromIndex(int dir) => dir switch
    {
        0 => "North",
        1 => "East",
        2 => "South",
        3 => "West",
        4 => "Up",
        5 => "Down",
        _ => "North"
    };

    private static string SexFromIndex(int value) => value switch
    {
        1 => "Male",
        2 => "Female",
        _ => "Neutral"
    };

    private static string PositionFromIndex(int value) => value switch
    {
        0 => "Dead",
        4 => "Sleeping",
        5 => "Resting",
        6 => "Sitting",
        8 => "Standing",
        _ => $"Position_{value}"
    };

    private static string AttackTypeFromIndex(int value) => value switch
    {
        500 => "Hit",
        _ => $"Attack_{value}"
    };

    private static string ApplyFromIndex(int value) => value switch
    {
        0 => "None",
        1 => "Strength",
        2 => "Dexterity",
        3 => "Intelligence",
        4 => "Wisdom",
        5 => "Constitution",
        6 => "Charisma",
        _ => $"Apply_{value}"
    };

    private static string ItemTypeFromIndex(int value) => value switch
    {
        1 => "Light",
        2 => "Scroll",
        3 => "Wand",
        4 => "Staff",
        5 => "Weapon",
        6 => "FireWeapon",
        7 => "Missile",
        8 => "Treasure",
        9 => "Armor",
        10 => "Potion",
        11 => "Worn",
        12 => "Other",
        13 => "Trash",
        14 => "Trap",
        15 => "Container",
        16 => "Note",
        17 => "DrinkContainer",
        18 => "Key",
        19 => "Food",
        20 => "Money",
        21 => "Pen",
        22 => "Boat",
        23 => "Fountain",
        24 => "Bomb",
        25 => "RawFood",
        26 => "Portal",
        27 => "Board",
        _ => $"Item_{value}"
    };

    private static string AntiClassFromIndex(int value) => value switch
    {
        0 => "None",
        _ => value.ToString()
    };

    private static IReadOnlyList<string> ItemWearFlags(int value) => FlagsFromBits(value, ItemWearFlagNames);

    private static IReadOnlyList<string> ItemExtraFlags(int value) => FlagsFromBits(value, ItemExtraFlagNames);

    private static IReadOnlyList<string> RoomFlags(int value) => FlagsFromBits(value, RoomFlagNames);

    private static IReadOnlyList<string> ExitFlags(int value) => FlagsFromBits(value, ExitFlagNames);

    private static IReadOnlyList<string> MobFlags(int value) => FlagsFromBits(value, MobFlagNames);

    private static IReadOnlyList<string> AffectFlags(int value) => FlagsFromBits(value, AffectFlagNames);

    private static string SectorFromIndex(int value) => value switch
    {
        0 => "Inside",
        1 => "City",
        2 => "Field",
        3 => "Forest",
        4 => "Hills",
        5 => "Mountain",
        6 => "WaterSwim",
        7 => "WaterNoSwim",
        8 => "Underwater",
        9 => "Air",
        10 => "Void",
        11 => "Desert",
        12 => "FoulWaste",
        13 => "FoulMountain",
        14 => "IcyUnderwater",
        15 => "FoulWaterNoSwim",
        _ => $"Sector_{value}"
    };

    private static string MobRaceFromIndex(int value) => value switch
    {
        0 => "Undefined",
        1 => "Humanoid",
        2 => "Undead",
        3 => "Catbeast",
        4 => "Hound",
        5 => "Bearbeast",
        6 => "Bird",
        7 => "Mount",
        8 => "Giant",
        9 => "Dwarf",
        10 => "Illusion",
        11 => "MountFly",
        12 => "Demon",
        13 => "Flybeast",
        14 => "Fire",
        15 => "Water",
        16 => "Earth",
        17 => "Air",
        18 => "Dragon",
        19 => "Insect",
        _ => $"Race_{value}"
    };

    private static string MobClassFromIndex(int value) => value switch
    {
        0 => "Normal",
        1 => "MagicUser",
        2 => "Cleric",
        3 => "Thief",
        4 => "Warrior",
        _ => $"Class_{value}"
    };

    private static IReadOnlyList<string> FlagsFromBits(int value, IReadOnlyList<string> names)
    {
        var flags = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                flags.Add(names[i]);
            }
        }

        return flags;
    }

    private static List<string> SplitKeywords(string keywords)
    {
        return keywords
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(keyword => keyword.Trim())
            .Where(keyword => keyword.Length > 0)
            .ToList();
    }

    private static List<string> RoomFlagNames => new()
    {
        "Dark",
        "Death",
        "NoMob",
        "Indoors",
        "Lawful",
        "Neutral",
        "Chaotic",
        "NoMagic",
        "Tunnel",
        "Private",
        "GodRoom",
        "BfsMark",
        "ZeroMana",
        "Dispell",
        "Silent",
        "InAir",
        "Ocs",
        "PkOk",
        "Arena",
        "Regen",
        "NoTeleport",
        "NoScry",
        "NoFlee",
        "Damage",
        "NoTrack",
        "NoSweep",
        "NoScout",
        "NoSleep",
        "NoSummon",
        "NoQuit",
        "NoDrop"
    };

    private static List<string> ExitFlagNames => new()
    {
        "IsDoor",
        "Closed",
        "Locked",
        "ResetClosed",
        "ResetLocked",
        "PickProof",
        "Trap",
        "Wall",
        "BashProof",
        "MagicProof",
        "PassProof",
        "TrapSet",
        "Secret",
        "Broken"
    };

    private static List<string> MobFlagNames => new()
    {
        "Spec",
        "Sentinel",
        "Scavenger",
        "IsNpc",
        "NiceThief",
        "Aggressive",
        "StayZone",
        "Wimpy",
        "AggressiveEvil",
        "AggressiveGood",
        "AggressiveNeutral",
        "Memory",
        "Helper",
        "Switched",
        "Blinder",
        "Hidden",
        "NoTrack"
    };

    private static List<string> AffectFlagNames => new()
    {
        "Blind",
        "Invisible",
        "DetectAlign",
        "DetectInvis",
        "DetectMagic",
        "SenseLife",
        "Hold",
        "Sanctuary",
        "Group",
        "Curse",
        "Light",
        "Poison",
        "ProtectEvil",
        "Paralysis",
        "Insanity",
        "FlWpn",
        "Sleep",
        "Dodge",
        "Sneak",
        "Hide",
        "Fear",
        "Charm",
        "FeignDeath",
        "Disguise",
        "Infrared",
        "Berzerk",
        "Hover",
        "Fly",
        "BreathWater",
        "Regeneration",
        "Chaos"
    };

    private static List<string> ItemWearFlagNames => new()
    {
        "Take",
        "FingerRight",
        "FingerLeft",
        "Neck1",
        "Neck2",
        "Body",
        "Head",
        "Legs",
        "Feet",
        "Hands",
        "Arms",
        "Shield",
        "About",
        "Waist",
        "WristRight",
        "WristLeft",
        "Wield",
        "Hold",
        "Throw",
        "WieldTwoHanded"
    };

    private static List<string> ItemExtraFlagNames => new()
    {
        "Glow",
        "Hum",
        "Dark",
        "Lock",
        "Evil",
        "Invisible",
        "Magic",
        "NoDrop",
        "Bless",
        "AntiGood",
        "AntiEvil",
        "AntiNeutral",
        "NoRent",
        "NoDonate",
        "NoInvis",
        "Hidden",
        "Broken",
        "Chaotic",
        "Arena",
        "Donated",
        "Flame",
        "NoLocate",
        "NoBreak",
        "NoRemove",
        "Quest",
        "NoSweep",
        "Killer"
    };


    private sealed class LegacyParser
    {
        private readonly TextReader _reader;
        private string? _bufferedToken;

        public LegacyParser(TextReader reader)
        {
            _reader = reader;
        }

        public string? ReadToken()
        {
            if (_bufferedToken is not null)
            {
                var token = _bufferedToken;
                _bufferedToken = null;
                return token;
            }

            var builder = new StringBuilder();
            int next;
            do
            {
                next = _reader.Read();
                if (next == -1)
                {
                    return null;
                }
            } while (char.IsWhiteSpace((char)next));

            do
            {
                builder.Append((char)next);
                next = _reader.Read();
                if (next == -1)
                {
                    break;
                }
            } while (!char.IsWhiteSpace((char)next));

            return builder.ToString();
        }

        public void PushToken(string token)
        {
            _bufferedToken = token;
        }

        public int ReadNumber()
        {
            var token = ReadToken();
            if (token is null)
            {
                return 0;
            }

            return ParseLegacyNumber(token);
        }

        public bool TryReadNumber(out int value)
        {
            var token = ReadToken();
            if (token is null)
            {
                value = 0;
                return false;
            }

            if (token.StartsWith('#') || token == "$")
            {
                PushToken(token);
                value = 0;
                return false;
            }

            value = ParseLegacyNumber(token);
            return true;
        }

        public string ReadTildeString()
        {
            var builder = new StringBuilder();
            int next;
            while ((next = _reader.Read()) != -1)
            {
                if (next == '~')
                {
                    break;
                }

                if (next == '\r')
                {
                    continue;
                }

                builder.Append((char)next);
            }

            return builder.ToString().TrimEnd();
        }

        public string? ReadLineSkippingWhitespace()
        {
            string? line;
            do
            {
                line = _reader.ReadLine();
                if (line is null)
                {
                    return null;
                }
            } while (line.Length == 0);

            return line.TrimStart();
        }

        public string ReadProgramBlock()
        {
            var builder = new StringBuilder();
            string? line;
            while ((line = _reader.ReadLine()) is not null)
            {
                if (line.StartsWith("~"))
                {
                    break;
                }

                builder.AppendLine(line);
            }

            return builder.ToString().TrimEnd();
        }

        public string ReadDiceString()
        {
            var token = ReadToken();
            return token ?? string.Empty;
        }
    }

    private static int ParseLegacyNumber(string token)
    {
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            return int.TryParse(token, out var value) ? value : 0;
        }

        if (token.All(char.IsDigit))
        {
            return int.TryParse(token, out var value) ? value : 0;
        }

        var parts = token.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var valueSum = 0;
        foreach (var part in parts)
        {
            var value = 0;
            foreach (var ch in part)
            {
                if (char.IsLower(ch))
                {
                    value |= 1 << (ch - 'a');
                }
                else if (char.IsUpper(ch))
                {
                    value |= 1 << (26 + (ch - 'A'));
                }
                else if (char.IsDigit(ch))
                {
                    value = value * 10 + (ch - '0');
                }
            }

            valueSum += value;
        }

        return valueSum;
    }

    private static List<string> SplitTokens(string line)
    {
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void SkipMobRecord(LegacyParser parser)
    {
        for (var i = 0; i < 40; i++)
        {
            var token = parser.ReadToken();
            if (token is null)
            {
                break;
            }

            if (token.StartsWith('#') || token == "$")
            {
                parser.PushToken(token);
                break;
            }
        }
    }

    private static void SkipObjectRecord(LegacyParser parser)
    {
        for (var i = 0; i < 40; i++)
        {
            var token = parser.ReadToken();
            if (token is null)
            {
                break;
            }

            if (token.StartsWith('#') || token == "$")
            {
                parser.PushToken(token);
                break;
            }
        }
    }

    private static string? ExtractComment(string line)
    {
        var index = line.IndexOf('*');
        if (index <= 0)
        {
            return null;
        }

        return line[(index + 1)..].Trim();
    }

    private sealed class RoomsFile
    {
        public RoomsFile(List<RoomContent> rooms)
        {
            this.rooms = rooms;
        }

        public List<RoomContent> rooms { get; }
    }

    private sealed record RoomContent(
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

    private sealed record ExitContent(
        string Direction,
        int TargetId,
        string Description,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> ExitFlags,
        int? KeyId);

    private sealed record ExtraDescriptionContent(IReadOnlyList<string> Keywords, string Description);

    private sealed class ZonesFile
    {
        public ZonesFile(List<ZoneContent> zones)
        {
            this.zones = zones;
        }

        public List<ZoneContent> zones { get; }
    }

    private sealed record ZoneContent(
        int Id,
        string Name,
        int TopRoomId,
        int Lifespan,
        string ResetMode,
        IReadOnlyList<ZoneResetCommandContent> ResetCommands);

    private sealed record ZoneResetCommandContent(
        string Command,
        int IfFlag,
        int Arg1,
        int Arg2,
        int? Arg3,
        string? Comment);

    private sealed class MobsFile
    {
        public MobsFile(List<MobContent> mobs)
        {
            this.mobs = mobs;
        }

        public List<MobContent> mobs { get; }
    }

    private sealed class MobContent
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

    private sealed record StatContent(
        int Strength,
        int Dexterity,
        int Intelligence,
        int Wisdom,
        int Constitution,
        int Charisma);

    private sealed record MobResourceContent(string HitDice, int Mana, int Move);

    private sealed record MobCombatContent(int Armor, int Hitroll, int Damroll);

    private sealed record MobAttackContent(
        string Type,
        int DamageType,
        int Chance,
        string DamageDice);

    private sealed class ObjectsFile
    {
        public ObjectsFile(List<ObjectContent> objects)
        {
            this.objects = objects;
        }

        public List<ObjectContent> objects { get; }
    }

    private sealed record ObjectContent(
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

    private sealed record ObjectAffectContent(string Location, int Modifier);
}
