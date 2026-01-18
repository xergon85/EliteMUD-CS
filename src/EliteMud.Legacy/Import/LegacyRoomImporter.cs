namespace EliteMud.Legacy.Import;

internal static class LegacyRoomImporter
{
    public static List<RoomContent> Load(string roomsPath, CancellationToken cancellationToken)
    {
        var rooms = new List<RoomContent>();
        foreach (var file in Directory.EnumerateFiles(roomsPath, "*.wld"))
        {
            using var reader = LegacyImportUtilities.CreateReader(file);
            var parser = new LegacyImportParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > LegacyImportConstants.MaxIterations)
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

                var vnum = LegacyImportLookup.ParseInt(token[1..]);
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
                    if (sectionIterations++ > LegacyImportConstants.MaxIterations)
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
                        var dir = LegacyImportLookup.ParseInt(marker[1..]);
                        var exitDesc = parser.ReadTildeString();
                        var keywords = parser.ReadTildeString();
                        var exitFlags = parser.ReadNumber();
                        var keyId = parser.ReadNumber();
                        var toRoom = parser.ReadNumber();

                        exits.Add(new ExitContent(
                            LegacyImportLookup.DirectionFromIndex(dir),
                            toRoom,
                            exitDesc,
                            LegacyImportUtilities.SplitKeywords(keywords),
                            LegacyImportLookup.ExitFlags(exitFlags),
                            keyId < 0 ? null : keyId));
                        continue;
                    }

                    if (marker == "E")
                    {
                        var keywords = parser.ReadTildeString();
                        var extraDesc = parser.ReadTildeString();
                        extras.Add(new ExtraDescriptionContent(LegacyImportUtilities.SplitKeywords(keywords), extraDesc));
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
                    LegacyImportLookup.SectorFromIndex(sector),
                    LegacyImportLookup.RoomFlags(roomFlags),
                    exits,
                    extras,
                    specialProc,
                    roomPrograms,
                    crashRoom));
            }
        }

        return rooms;
    }
}
