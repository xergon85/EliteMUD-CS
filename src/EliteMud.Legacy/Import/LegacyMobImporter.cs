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

    private static void SkipMobRecord(LegacyImportParser parser)
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
}
