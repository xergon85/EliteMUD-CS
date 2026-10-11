namespace EliteMud.Legacy.Import;

internal static class LegacyRoomImporter
{
    public static List<RoomContent> Load(
        IReadOnlyList<LegacySourceFile> files,
        LegacyImportLog log,
        CancellationToken cancellationToken)
    {
        var rooms = new LegacyRecordSet<RoomContent>("room", log);
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
                    throw new InvalidOperationException($"Room import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }

                // Legacy end-of-file marker; some files write it as "$~"
                if (token.StartsWith('$'))
                {
                    break;
                }

                if (!token.StartsWith('#'))
                {
                    log.Warn("Stray text", source.Name, $"ignored '{token}' between room records");
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
                var roomPrograms = new List<RoomProgramContent>();
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

                    // Legacy: load_rooms() in db.c:878-898 dispatches on the marker's first
                    // character, case-sensitive. Unknown markers are skipped (and logged here).
                    switch (marker[0])
                    {
                        case 'S':
                            break;

                        case 'D':
                            exits.Add(ReadExit(parser, marker));
                            continue;

                        case 'E':
                        {
                            var keywords = parser.ReadTildeString();
                            var extraDesc = parser.ReadTildeString();
                            extras.Add(new ExtraDescriptionContent(LegacyImportUtilities.SplitKeywords(keywords), extraDesc));
                            continue;
                        }

                        case 'C':
                            crashRoom = true;
                            continue;

                        case 'P':
                            // Legacy: special_assign() reads the name from the marker itself, e.g. "P(bank)"
                            specialProc = ParseSpecialName(marker);
                            if (specialProc is null)
                            {
                                log.Warn("Room markers", source.Name, $"room #{vnum}: unreadable special '{marker}'");
                            }

                            continue;

                        case '>':
                            // Legacy: rprog_read_programs() in db.c:3595. The type is part of the
                            // marker (">push"), followed by tilde-terminated arguments and commands.
                            roomPrograms.Add(new RoomProgramContent(
                                marker[1..],
                                parser.ReadTildeString(),
                                parser.ReadTildeString()));
                            continue;

                        default:
                            log.Warn("Room markers", source.Name, $"room #{vnum}: ignored unknown marker '{marker}'");
                            continue;
                    }

                    break;
                }

                rooms.Add(vnum, new RoomContent(
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
                    crashRoom,
                    source.Name), source);
            }
        }

        return rooms.ToList();
    }

    /// <summary>
    /// Apply the config's data fixes to the imported rooms. Every fix is logged; one whose room or exit
    /// doesn't exist is a warning.
    /// </summary>
    public static List<RoomContent> ApplyFixes(List<RoomContent> rooms, LegacyImportConfig config, LegacyImportLog log)
    {
        foreach (var fix in config.Fixes)
        {
            var source = $"room #{fix.Room} {fix.Exit}";
            var index = rooms.FindIndex(room => room.Id == fix.Room);
            var exitIndex = index < 0
                ? -1
                : rooms[index].Exits.ToList().FindIndex(exit => string.Equals(exit.Direction, fix.Exit, StringComparison.OrdinalIgnoreCase));
            if (exitIndex < 0)
            {
                log.Warn("Fix not applied", source, $"room or exit not found ({fix.Reason})");
                continue;
            }

            var exits = rooms[index].Exits.ToList();
            var flags = LegacyImportLookup.ExitFlags(DoorTypeToFlags(fix.DoorType));
            log.Info("Fix applied", source,
                $"door type {fix.DoorType}: [{string.Join(", ", exits[exitIndex].ExitFlags)}] -> [{string.Join(", ", flags)}] ({fix.Reason})");
            exits[exitIndex] = exits[exitIndex] with { ExitFlags = flags };
            rooms[index] = rooms[index] with { Exits = exits };
        }

        return rooms;
    }

    private const int ExitIsDoor = 1 << 0;
    private const int ExitPickProof = 1 << 5;
    private const int NoRoom = -1;

    // Legacy: setup_dir() in db.c:947
    private static ExitContent ReadExit(LegacyImportParser parser, string marker)
    {
        var dir = LegacyImportLookup.ParseInt(marker[1..]);
        var exitDesc = parser.ReadTildeString();
        var keywords = parser.ReadTildeString();

        var exitFlags = DoorTypeToFlags(parser.ReadNumber());

        // Legacy reads these with fscanf(" %d "), which stops at a glued room terminator ("28701S").
        // If the value isn't a number at all, the exit leads nowhere rather than to room 0.
        var keyId = parser.ReadInteger(fallback: -1);
        var toRoom = parser.ReadInteger(fallback: NoRoom);

        return new ExitContent(
            LegacyImportLookup.DirectionFromIndex(dir),
            toRoom,
            exitDesc,
            LegacyImportUtilities.SplitKeywords(keywords),
            LegacyImportLookup.ExitFlags(exitFlags),
            keyId < 0 ? null : keyId);
    }

    // Door type: 1 = door, 2 = pick-proof door, anything else is raw EX_* bits (db.c:958-967)
    private static int DoorTypeToFlags(int doorType) => doorType switch
    {
        1 => ExitIsDoor,
        2 => ExitIsDoor | ExitPickProof,
        var bits => bits
    };

    private static string? ParseSpecialName(string marker)
    {
        var open = marker.IndexOf('(');
        var close = marker.IndexOf(')', open + 1);
        if (open < 0 || close <= open + 1)
        {
            return null;
        }

        return marker[(open + 1)..close];
    }
}
