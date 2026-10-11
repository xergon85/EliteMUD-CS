namespace EliteMud.Legacy.Import;

internal static class LegacyZoneImporter
{
    public static List<ZoneContent> Load(
        IReadOnlyList<LegacySourceFile> files,
        LegacyImportConfig config,
        LegacyImportLog log,
        CancellationToken cancellationToken)
    {
        var zones = new LegacyRecordSet<ZoneContent>("zone", log);
        foreach (var source in files)
        {
            var file = source.Path;
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
                if (name.StartsWith('$'))
                {
                    // Legacy end-of-file marker: a zone number followed by "$~" (load_zones() in db.c)
                    break;
                }

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
                        log.Warn("Zone resets", source.Name, $"zone #{zoneId}: skipped unparseable reset line: {line.Trim()}");
                        continue;
                    }

                    commands.Add(command);
                }

                // Zones the original didn't load stay inactive unless the config says they're live
                var active = source.IsIndexed || config.ActiveZones.Contains(zoneId);
                zones.Add(zoneId, new ZoneContent(
                    zoneId,
                    name,
                    topRoom,
                    lifespan,
                    LegacyImportLookup.ResetMode(resetMode),
                    commands,
                    active,
                    source.Name), source);
            }
        }

        return zones.ToList();
    }
}
