using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyZoneResetParserTests
{
    [Fact]
    public void TryParse_TabSeparatedComment_KeepsArg3()
    {
        // Legacy 30.zon line 97: the Midgaard jeweler
        var parsed = LegacyZoneResetParser.TryParse("M 0 3031 1 3072\t\tJeweler", out var command);

        Assert.True(parsed);
        Assert.NotNull(command);
        Assert.Equal("M", command.Command);
        Assert.Equal(0, command.IfFlag);
        Assert.Equal(3031, command.Arg1);
        Assert.Equal(1, command.Arg2);
        Assert.Equal(3072, command.Arg3);
        Assert.Equal("Jeweler", command.Comment);
    }

    [Fact]
    public void TryParse_TabsBetweenArguments_ParsesAllArguments()
    {
        var parsed = LegacyZoneResetParser.TryParse("E\t1\t3022\t100\t16", out var command);

        Assert.True(parsed);
        Assert.Equal(3022, command!.Arg1);
        Assert.Equal(100, command.Arg2);
        Assert.Equal(16, command.Arg3);
        Assert.Null(command.Comment);
    }

    [Fact]
    public void TryParse_StarComment_StripsMarker()
    {
        var parsed = LegacyZoneResetParser.TryParse("M 0 1 1 0 * load mob 1 in room 0", out var command);

        Assert.True(parsed);
        Assert.Equal(0, command!.Arg3);
        Assert.Equal("load mob 1 in room 0", command.Comment);
    }

    [Theory]
    [InlineData("G 1 3031 100 5 a numeric comment")]
    [InlineData("R 0 3001 3010 99")]
    public void TryParse_CommandsWithoutArg3_TreatTrailingNumberAsComment(string line)
    {
        var parsed = LegacyZoneResetParser.TryParse(line, out var command);

        Assert.True(parsed);
        Assert.Null(command!.Arg3);
        Assert.NotNull(command.Comment);
    }

    [Theory]
    [InlineData("M 0 3031 1")]          // M requires arg3
    [InlineData("M 0 abc 1 3072")]      // non-numeric argument
    [InlineData("O 0 3000")]            // too few arguments
    [InlineData("MOB 0 1 1 1")]         // command must be one letter
    public void TryParse_MalformedLine_ReturnsFalse(string line)
    {
        var parsed = LegacyZoneResetParser.TryParse(line, out var command);

        Assert.False(parsed);
        Assert.Null(command);
    }
}
