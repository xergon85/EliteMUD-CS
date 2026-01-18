using System.Linq;

namespace EliteMud.Legacy.Import;

internal static class LegacyMobImporter
{
    public static List<MobContent> Load(string mobsPath, CancellationToken cancellationToken)
    {
        var mobs = new List<MobContent>();
        foreach (var file in Directory.EnumerateFiles(mobsPath, "*.mob"))
        {
            using var reader = LegacyImportUtilities.CreateReader(file);
            var parser = new LegacyImportParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > LegacyImportConstants.MaxIterations)
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

                var vnum = LegacyImportLookup.ParseInt(token[1..]);
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
                    Race = LegacyImportLookup.MobRaceFromIndex(race),
                    Class = LegacyImportLookup.MobClassFromIndex(mobClass),
                    Flags = LegacyImportLookup.MobFlags(flags),
                    Affects = LegacyImportLookup.AffectFlags(affects),
                    Alignment = alignment,
                    Stats = new StatContent(10, 10, 10, 10, 10, 10),
                    Resources = new MobResourceContent("", 0, 0),
                    Combat = new MobCombatContent(0, 0, 0),
                    Skills = new List<string>(),
                    Resistances = new List<string>(),
                    Attacks = new List<MobAttackContent>(),
                    DefaultPosition = "Standing",
                    Sex = "Neutral",
                    Programs = new List<string>()
                };

                ParseMobBody(parser, mob, format);
                ParseMobExtensions(parser, mob);
                mobs.Add(mob);
            }
        }

        return mobs;
    }

    private static void ParseMobBody(LegacyImportParser parser, MobContent mob, string format)
    {
        if (string.Equals(format, "S", StringComparison.OrdinalIgnoreCase))
        {
            ParseSimpleMob(parser, mob);
            return;
        }

        if (string.Equals(format, "A", StringComparison.OrdinalIgnoreCase))
        {
            ParseAutoMob(parser, mob);
            return;
        }

        parser.PushToken(format);
        ParseLegacyMob(parser, mob);
    }

    private static void ParseSimpleMob(LegacyImportParser parser, MobContent mob)
    {
        var level = parser.ReadNumber();
        var hitrollBase = parser.ReadNumber();
        var armorBase = parser.ReadNumber();
        var hitDice = parser.ReadDiceString();

        mob.Level = level;
        mob.Combat = new MobCombatContent(armorBase * 10, 20 - hitrollBase, mob.Combat.Damroll);
        mob.Resources = new MobResourceContent(hitDice, 100 + level * 10, 80 + level * 15);

        ParseAttacks(parser, mob);
        ParseSkills(parser, mob);
        ParseResistances(parser, mob);

        mob.Gold = parser.ReadNumber();
        mob.Experience = parser.ReadNumber();
        parser.ReadNumber();
        mob.DefaultPosition = LegacyImportLookup.PositionFromIndex(parser.ReadNumber());
        mob.Sex = LegacyImportLookup.SexFromIndex(parser.ReadNumber());
    }

    private static void ParseAutoMob(LegacyImportParser parser, MobContent mob)
    {
        var level = parser.ReadNumber();
        var sex = parser.ReadNumber();

        mob.Level = level;
        mob.Sex = LegacyImportLookup.SexFromIndex(sex);
        mob.DefaultPosition = "Standing";
    }

    private static void ParseLegacyMob(LegacyImportParser parser, MobContent mob)
    {
        var strength = parser.ReadNumber();
        var intelligence = parser.ReadNumber();
        var wisdom = parser.ReadNumber();
        var dexterity = parser.ReadNumber();
        var constitution = parser.ReadNumber();
        var charisma = parser.ReadNumber();

        parser.ReadNumber();
        parser.ReadNumber();
        var armor = parser.ReadNumber();
        var maxMana = parser.ReadNumber();
        var maxMove = parser.ReadNumber();
        var gold = parser.ReadNumber();
        var experience = parser.ReadNumber();
        parser.ReadNumber();
        var defaultPosition = parser.ReadNumber();
        var sex = parser.ReadNumber();
        var level = parser.ReadNumber();
        parser.ReadNumber();
        parser.ReadNumber();
        parser.ReadNumber();

        parser.ReadNumber();
        parser.ReadNumber();
        parser.ReadNumber();

        var resistances = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            resistances.Add(parser.ReadNumber());
        }

        mob.Level = level;
        mob.Stats = new StatContent(strength, dexterity, intelligence, wisdom, constitution, charisma);
        mob.Resources = new MobResourceContent("", maxMana, maxMove);
        mob.Combat = new MobCombatContent(armor * 10, mob.Combat.Hitroll, mob.Combat.Damroll);
        mob.Gold = gold;
        mob.Experience = experience;
        mob.DefaultPosition = LegacyImportLookup.PositionFromIndex(defaultPosition);
        mob.Sex = LegacyImportLookup.SexFromIndex(sex);

        ParseAttacks(parser, mob);
        ParseSkills(parser, mob);
        ParseResistances(parser, mob);

        if (mob.Resistances.Count == 0)
        {
            mob.Resistances = resistances
                .Select((value, index) => $"{LegacyImportLookup.ResistanceLabel(index)}:{value}")
                .ToList();
        }
    }

    private static void ParseAttacks(LegacyImportParser parser, MobContent mob)
    {
        var token = parser.ReadToken();
        if (token is null)
        {
            return;
        }

        var attackType = LegacyImportLookup.ParseInt(token);
        if (attackType == -1)
        {
            return;
        }

        var attacks = new List<MobAttackContent>();
        var iterations = 0;
        while (attackType != -1 && iterations++ < LegacyImportConstants.MaxIterations)
        {
            var damageType = parser.ReadNumber();
            var chance = parser.ReadNumber();
            var damageDice = parser.ReadDiceString();

            attacks.Add(new MobAttackContent(
                LegacyImportLookup.AttackTypeFromIndex(attackType),
                damageType,
                chance,
                damageDice));

            var nextToken = parser.ReadToken();
            if (nextToken is null)
            {
                break;
            }

            attackType = LegacyImportLookup.ParseInt(nextToken);
        }

        mob.Attacks = attacks;
    }

    private static void ParseSkills(LegacyImportParser parser, MobContent mob)
    {
        var token = parser.ReadToken();
        if (token is null)
        {
            return;
        }

        var skillType = LegacyImportLookup.ParseInt(token);
        if (skillType == -1)
        {
            return;
        }

        var skills = new List<string>();
        var iterations = 0;
        while (skillType != -1 && iterations++ < LegacyImportConstants.MaxIterations)
        {
            var percentage = parser.ReadNumber();
            skills.Add($"{LegacyImportLookup.SkillLabel(skillType)}:{percentage}");

            var nextToken = parser.ReadToken();
            if (nextToken is null)
            {
                break;
            }

            skillType = LegacyImportLookup.ParseInt(nextToken);
        }

        mob.Skills = skills;
    }

    private static void ParseResistances(LegacyImportParser parser, MobContent mob)
    {
        var token = parser.ReadToken();
        if (token is null)
        {
            return;
        }

        var resistanceType = LegacyImportLookup.ParseInt(token);
        if (resistanceType == -1)
        {
            return;
        }

        var resistances = new List<string>();
        var iterations = 0;
        while (resistanceType != -1 && iterations++ < LegacyImportConstants.MaxIterations)
        {
            var percentage = parser.ReadNumber();
            resistances.Add($"{LegacyImportLookup.ResistanceLabel(resistanceType)}:{percentage}");

            var nextToken = parser.ReadToken();
            if (nextToken is null)
            {
                break;
            }

            resistanceType = LegacyImportLookup.ParseInt(nextToken);
        }

        mob.Resistances = resistances;
    }

    private static void ParseMobExtensions(LegacyImportParser parser, MobContent mob)
    {
        while (true)
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

            if (token == "A")
            {
                var action = parser.ReadTildeString();
                mob.ActionScript = action.Length == 0 ? null : action;
                continue;
            }

            if (token.StartsWith("P", StringComparison.Ordinal))
            {
                var special = token.Length > 1 ? token[1..] : parser.ReadToken();
                if (special is not null)
                {
                    special = special.Trim();
                    if (special.StartsWith("(", StringComparison.Ordinal) && special.EndsWith(")", StringComparison.Ordinal))
                    {
                        special = special[1..^1];
                    }
                }

                mob.SpecialProc = special;
                continue;
            }

            if (token == ">")
            {
                mob.Programs.Add(parser.ReadProgramBlock());
            }
        }
    }
}
