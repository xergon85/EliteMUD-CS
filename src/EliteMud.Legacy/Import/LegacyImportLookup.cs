namespace EliteMud.Legacy.Import;

internal static class LegacyImportLookup
{
    public static int ParseInt(string token)
    {
        return int.TryParse(token, out var value) ? value : 0;
    }

    public static string ResetMode(int mode) => mode switch
    {
        0 => "ResetNever",
        1 => "ResetWhenEmpty",
        2 => "ResetAlways",
        _ => "ResetAlways"
    };

    public static string DirectionFromIndex(int dir) => dir switch
    {
        0 => "North",
        1 => "East",
        2 => "South",
        3 => "West",
        4 => "Up",
        5 => "Down",
        _ => "North"
    };

    public static string SexFromIndex(int value) => value switch
    {
        1 => "Male",
        2 => "Female",
        _ => "Neutral"
    };

    public static string PositionFromIndex(int value) => value switch
    {
        0 => "Dead",
        1 => "MortallyWounded",
        2 => "Incapacitated",
        3 => "Stunned",
        4 => "Sleeping",
        5 => "Resting",
        6 => "Sitting",
        7 => "Fighting",
        8 => "Standing",
        10 => "Swimming",
        11 => "Diving",
        12 => "Hovering",
        13 => "Flying",
        _ => $"Position_{value}"
    };

    public static string AttackTypeFromIndex(int value) => value switch
    {
        400 => "Hit",
        401 => "Bludgeon",
        402 => "Pierce",
        403 => "Slash",
        404 => "Blast",
        405 => "Whip",
        406 => "NoBsPierce",
        407 => "Claw",
        408 => "Bite",
        409 => "Sting",
        410 => "Crush",
        411 => "Ama",
        _ => $"Attack_{value}"
    };

    public static string ApplyFromIndex(int value) => value switch
    {
        0 => "None",
        1 => "Strength",
        2 => "Dexterity",
        3 => "Intelligence",
        4 => "Wisdom",
        5 => "Constitution",
        6 => "Charisma",
        7 => "Class",
        8 => "Level",
        9 => "Age",
        10 => "Weight",
        11 => "Height",
        12 => "Mana",
        13 => "Hit",
        14 => "Move",
        15 => "Gold",
        16 => "Experience",
        17 => "Armor",
        18 => "Hitroll",
        19 => "Damroll",
        20 => "SavingPhysical",
        21 => "SavingMental",
        22 => "SavingMagic",
        23 => "SavingPoison",
        24 => "MagicResistance",
        25 => "Jump",
        26 => "Steal",
        27 => "Sneak",
        28 => "Track",
        29 => "Archery",
        30 => "Throw",
        31 => "Swim",
        32 => "Dive",
        33 => "Bitvector2",
        _ => $"Apply_{value}"
    };

    public static string ItemTypeFromIndex(int value) => value switch
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

    public static string AntiClassFromIndex(int value) => value switch
    {
        0 => "None",
        _ => value.ToString()
    };

    public static IReadOnlyList<string> ItemWearFlags(int value) => FlagsFromBits(value, ItemWearFlagNames);

    public static IReadOnlyList<string> ItemExtraFlags(int value) => FlagsFromBits(value, ItemExtraFlagNames);

    public static IReadOnlyList<string> ContainerFlags(int value) => FlagsFromBits(value, ContainerFlagNames);

    public static IReadOnlyList<string> PortalFlags(int value) => FlagsFromBits(value, PortalFlagNames);

    public static IReadOnlyList<string> RoomFlags(int value) => FlagsFromBits(value, RoomFlagNames);

    public static IReadOnlyList<string> ExitFlags(int value) => FlagsFromBits(value, ExitFlagNames);

    public static IReadOnlyList<string> MobFlags(int value) => FlagsFromBits(value, MobFlagNames);

    public static IReadOnlyList<string> AffectFlags(int value) => FlagsFromBits(value, AffectFlagNames);

    public static string SectorFromIndex(int value) => value switch
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

    public static string MobRaceFromIndex(int value) => value switch
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

    public static string MobClassFromIndex(int value) => value switch
    {
        0 => "Normal",
        1 => "MagicUser",
        2 => "Cleric",
        3 => "Thief",
        4 => "Warrior",
        _ => $"Class_{value}"
    };

    public static string SkillLabel(int value) => $"Skill_{value}";

    public static string ResistanceLabel(int value) => $"Resist_{value}";

    public static string BitvectorLabel(int value) => $"Bitvector_{value}";

    public static IReadOnlyList<string> FlagsFromBits(int value, IReadOnlyList<string> names)
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

    private static List<string> ContainerFlagNames => new()
    {
        "Closeable",
        "PickProof",
        "Closed",
        "Locked",
        "Trap"
    };

    private static List<string> PortalFlagNames => new()
    {
        "Closed",
        "Locked",
        "Random",
        "Effect"
    };
}
