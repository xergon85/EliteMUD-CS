using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Game;

/// <summary>
/// Legacy descriptions are hand-wrapped, so their line breaks are kept and sent as telnet line endings.
/// </summary>
public class TextCleanerTests
{
    [Fact]
    public void CleanMultiline_KeepsLegacyLineBreaks()
    {
        var text = "You are in a small room\r\nwith wooden desks.  \n\tA door leads east.\n\n";

        Assert.Equal("You are in a small room\nwith wooden desks.\n A door leads east.", TextCleaner.CleanMultiline(text));
    }

    [Fact]
    public void CleanMultiline_TurnsEscapedNewlinesIntoLineBreaks()
    {
        Assert.Equal("first\nsecond", TextCleaner.CleanMultiline("first\\nsecond"));
    }

    [Fact]
    public void Clean_StillFlattensSingleLineFields()
    {
        Assert.Equal("a small rat", TextCleaner.Clean("a small\nrat"));
    }

    [Fact]
    public void RoomDescription_KeepsItsLines()
    {
        var room = new RoomDefinition(1, "Temple", "line one\nline two", new List<ExitDefinition>());

        Assert.Equal("line one\nline two", room.Description);
    }

    [Fact]
    public void TempleOfMidgaard_KeepsItsLegacyLines()
    {
        var lines = RealWorld.State.World.GetRoom(3001).Description.Split('\n');

        Assert.True(lines.Length > 1);
        Assert.All(lines, line => Assert.True(line.Length <= 80, $"line longer than 80: {line}"));
    }

    [Theory]
    [InlineData("one\ntwo", "one\r\ntwo")]
    [InlineData("one\r\ntwo", "one\r\ntwo")]
    [InlineData("no breaks", "no breaks")]
    public void Telnet_SendsCrLfLineEndings(string message, string expected)
    {
        Assert.Equal(expected, TelnetSession.ToTelnetLineEndings(message));
    }
}
