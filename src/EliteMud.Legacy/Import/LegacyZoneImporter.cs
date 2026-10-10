namespace EliteMud.Legacy.Import;

internal static class LegacyZoneImporter
{
    public static List<ZoneContent> Load(string zonesPath, CancellationToken cancellationToken)
    {
        var zones = new List<ZoneContent>();
        foreach (var file in Directory.EnumerateFiles(zonesPath, "*.zon"))
        {
            using var reader = LegacyImportUtilities.CreateReader(file);
            var parser = new LegacyImportParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > LegacyImportConstants.MaxIterations)
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

                var zoneId = LegacyImportLookup.ParseInt(token[1..]);
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
                    if (commandIterations++ > LegacyImportConstants.MaxIterations)
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

                    // Legacy skips blank lines (fscanf(" ") in load_reset_cmds)
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("*"))
                    {
                        continue;
                    }

                    if (!LegacyZoneResetParser.TryParse(line, out var command))
                    {
                        Console.WriteLine($"  Warning: {Path.GetFileName(file)} zone #{zoneId}: skipped unparseable reset line: {line.Trim()}");
                        continue;
                    }

                    commands.Add(command);
                }

                zones.Add(new ZoneContent(zoneId, name, topRoom, lifespan, LegacyImportLookup.ResetMode(resetMode), commands));
            }
        }

        return zones;
    }
}
