using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyImportParserTests
{
    [Fact]
    public void ReadTildeString_DoubleTilde_DiscardsRestOfTerminatorLine()
    {
        // Legacy 93.mob #9308 ends its description with "~~"
        var parser = Parser("She suffers here endlessly.\n~~\n0 4 82");

        Assert.Equal("She suffers here endlessly.\n~", parser.ReadTildeString());
        Assert.Equal(0, parser.ReadNumber());
        Assert.Equal(4, parser.ReadNumber());
    }

    [Fact]
    public void ReadTildeString_TildeInsideLine_IsContent()
    {
        var parser = Parser("Waves ~~~ roll in.\nThe end~\n42");

        Assert.Equal("Waves ~~~ roll in.\nThe end", parser.ReadTildeString());
        Assert.Equal(42, parser.ReadNumber());
    }

    [Fact]
    public void ReadDiceString_GluedTerminator_IsPushedBack()
    {
        // Legacy 5.mob #501: "400 500 100 3d6+9-1"
        var parser = Parser("3d6+9-1\n-1");

        Assert.Equal("3d6+9", parser.ReadDiceString());
        Assert.Equal(-1, parser.ReadNumber());
        Assert.Equal(-1, parser.ReadNumber());
    }

    [Fact]
    public void ReadNumber_GluedTerminator_IsPushedBack()
    {
        // Legacy 120.mob skill list: "384 385-1"
        var parser = Parser("385-1 -1");

        Assert.Equal(385, parser.ReadNumber());
        Assert.Equal(-1, parser.ReadNumber());
        Assert.Equal(-1, parser.ReadNumber());
    }

    [Fact]
    public void ReadNumber_FlagLetters_StillParseAsBits()
    {
        var parser = Parser("bg");

        Assert.Equal((1 << 1) | (1 << 6), parser.ReadNumber());
    }

    private static LegacyImportParser Parser(string text) => new(new StringReader(text));
}
