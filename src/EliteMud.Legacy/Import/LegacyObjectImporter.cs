using System;

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

                var extras = new List<ExtraDescriptionContent>();
                var affects = new List<ObjectAffectContent>();
                var bitvectors = new List<string>();
                string? specialProc = null;

                ParseObjectExtensions(parser, extras, affects, bitvectors, ref specialProc);

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
                    extras,
                    affects,
                    bitvectors,
                    specialProc));
            }
        }

        return objects;
    }

    private static void ParseObjectExtensions(
        LegacyImportParser parser,
        List<ExtraDescriptionContent> extras,
        List<ObjectAffectContent> affects,
        List<string> bitvectors,
        ref string? specialProc)
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

            if (token == "E")
            {
                var keywords = parser.ReadTildeString();
                var extraDesc = parser.ReadTildeString();
                extras.Add(new ExtraDescriptionContent(LegacyImportUtilities.SplitKeywords(keywords), extraDesc));
                continue;
            }

            if (token == "A")
            {
                var location = parser.ReadNumber();
                var modifier = parser.ReadNumber();
                affects.Add(new ObjectAffectContent(LegacyImportLookup.ApplyFromIndex(location), modifier));
                continue;
            }

            if (token == "B")
            {
                var bitvector = parser.ReadNumber();
                bitvectors.Add(LegacyImportLookup.BitvectorLabel(bitvector));
                continue;
            }

            if (token.StartsWith("P", StringComparison.Ordinal))
            {
                specialProc = token.Length > 1 ? token[1..] : parser.ReadToken();
                if (specialProc is not null)
                {
                    specialProc = specialProc.Trim();
                    if (specialProc.StartsWith("(", StringComparison.Ordinal) && specialProc.EndsWith(")", StringComparison.Ordinal))
                    {
                        specialProc = specialProc[1..^1];
                    }
                }
            }
        }
    }
}
