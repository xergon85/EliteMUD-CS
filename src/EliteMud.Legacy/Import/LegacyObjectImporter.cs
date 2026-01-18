namespace EliteMud.Legacy.Import;

internal static class LegacyObjectImporter
{
    public static List<ObjectContent> Load(string objectsPath, CancellationToken cancellationToken)
    {
        var objects = new List<ObjectContent>();
        foreach (var file in Directory.EnumerateFiles(objectsPath, "*.obj"))
        {
            using var reader = LegacyImportUtilities.CreateReader(file);
            var parser = new LegacyImportParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > LegacyImportConstants.MaxIterations)
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

                var vnum = LegacyImportLookup.ParseInt(token[1..]);
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
                    LegacyImportLookup.ItemTypeFromIndex(type),
                    level,
                    LegacyImportLookup.AntiClassFromIndex(antiClass),
                    LegacyImportLookup.ItemExtraFlags(extraFlags),
                    LegacyImportLookup.ItemWearFlags(wearFlags),
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

    private static void SkipObjectRecord(LegacyImportParser parser)
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
