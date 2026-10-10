using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyRoomImporterTests
{
    // Shape of legacy 77.wld room #7715: a room program whose body contains the word
    // "down", followed by the next room. The old parser read "down" as an exit marker
    // and swallowed room #7716.
    private const string RoomWithProgram = """
        #7715
        Northeast Side of the Hill~
           You are standing on the northeast side of a small hill.
        ~
        77 r 4
        D0
        The river makes a hard turn east just before the hill.
        ~
        ~
        0 -1 7709
        D4
        You see the top of the hill.
        ~
        ~
        0 -1 7737
        >push 10 1~
        You lose your balance and slide down the muddy hill side.
        ~
        S
        #7716
        On the River~
           The river flows quietly south.
        ~
        77 0 6
        D0
        The river gently flows from north to south.
        ~
        ~
        0 -1 7710
        S
        $~
        """;

    [Fact]
    public void Load_RoomProgram_IsParsedAndDoesNotSwallowNextRoom()
    {
        var rooms = LoadRooms(RoomWithProgram);

        Assert.Equal(new[] { 7715, 7716 }, rooms.Select(r => r.Id));

        var hill = rooms[0];
        Assert.Equal(new[] { "North", "Up" }, hill.Exits.Select(e => e.Direction));
        Assert.Equal(new[] { 7709, 7737 }, hill.Exits.Select(e => e.TargetId));

        var program = Assert.Single(hill.RoomPrograms);
        Assert.Equal("push", program.Type);
        Assert.Equal("10 1", program.Arguments);
        Assert.Equal("You lose your balance and slide down the muddy hill side.", program.Commands);

        var river = rooms[1];
        Assert.Equal("On the River", river.Name);
        Assert.Equal(7710, Assert.Single(river.Exits).TargetId);
    }

    [Fact]
    public void Load_SpecialMarker_ReadsNameFromMarker()
    {
        var rooms = LoadRooms("""
            #3000
            A Bank~
            Money.
            ~
            30 0 0
            P(bank)
            S
            $~
            """);

        Assert.Equal("bank", Assert.Single(rooms).SpecialProc);
    }

    private static List<RoomContent> LoadRooms(string wld)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"elitemud-wld-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "test.wld"), wld);
            return LegacyRoomImporter.Load(directory, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
