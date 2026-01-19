using Xunit;
using EliteMud.Server;

namespace EliteMud.Tests;

public sealed class ColorTranslatorTests
{
    [Fact]
    public void TranslateColors_NoColorCodes_ReturnsUnchanged()
    {
        var input = "Hello world!";
        var result = ColorTranslator.TranslateColors(input);
        Assert.Equal(input, result);
    }
    
    [Fact]
    public void TranslateColors_NullOrEmpty_ReturnsUnchanged()
    {
        Assert.Null(ColorTranslator.TranslateColors(null!));
        Assert.Equal("", ColorTranslator.TranslateColors(""));
    }
    
    [Fact]
    public void TranslateColors_NormalCode_ReturnsAnsiEscape()
    {
        var input = "#NNormal text";
        var result = ColorTranslator.TranslateColors(input);
        Assert.StartsWith("\x1B[0;37m", result);
        Assert.EndsWith("Normal text", result);
    }
    
    [Fact]
    public void TranslateColors_DarkColors_ReturnsCorrectAnsiCodes()
    {
        Assert.Contains("\x1B[0;31m", ColorTranslator.TranslateColors("#RRed"));
        Assert.Contains("\x1B[0;32m", ColorTranslator.TranslateColors("#GGreen"));
        Assert.Contains("\x1B[0;33m", ColorTranslator.TranslateColors("#YYellow"));
        Assert.Contains("\x1B[0;34m", ColorTranslator.TranslateColors("#BBlue"));
        Assert.Contains("\x1B[0;35m", ColorTranslator.TranslateColors("#MMagenta"));
        Assert.Contains("\x1B[0;36m", ColorTranslator.TranslateColors("#CCyan"));
        Assert.Contains("\x1B[0;37m", ColorTranslator.TranslateColors("#WWhite"));
    }
    
    [Fact]
    public void TranslateColors_LightColors_ReturnsCorrectAnsiCodes()
    {
        Assert.Contains("\x1B[1;31m", ColorTranslator.TranslateColors("#rBright red"));
        Assert.Contains("\x1B[1;32m", ColorTranslator.TranslateColors("#gBright green"));
        Assert.Contains("\x1B[1;33m", ColorTranslator.TranslateColors("#yBright yellow"));
        Assert.Contains("\x1B[1;34m", ColorTranslator.TranslateColors("#bBright blue"));
        Assert.Contains("\x1B[1;35m", ColorTranslator.TranslateColors("#mBright magenta"));
        Assert.Contains("\x1B[1;36m", ColorTranslator.TranslateColors("#cBright cyan"));
        Assert.Contains("\x1B[1;37m", ColorTranslator.TranslateColors("#wBright white"));
    }
    
    [Fact]
    public void TranslateColors_MultipleColors_ReturnsCorrectSequence()
    {
        var input = "#RRed #GGreen #BBlue#N";
        var result = ColorTranslator.TranslateColors(input);
        Assert.Contains("\x1B[0;31m", result); // Red
        Assert.Contains("\x1B[0;32m", result); // Green
        Assert.Contains("\x1B[0;34m", result); // Blue
        Assert.Contains("\x1B[0;37m", result); // Normal
    }
    
    [Fact]
    public void TranslateColors_EscapedHash_ReturnsLiteralHash()
    {
        var input = "Price: ##100";
        var result = ColorTranslator.TranslateColors(input);
        Assert.Contains("#100", result);
    }
    
    [Fact]
    public void TranslateColors_UnknownCode_KeepsHash()
    {
        var input = "#XUnknown";
        var result = ColorTranslator.TranslateColors(input);
        Assert.StartsWith("#X", result);
    }
    
    [Fact]
    public void TranslateColors_ScoreCommandSample_TranslatesCorrectly()
    {
        // Sample from score command
        var input = "You are #BTestChar#N#C the newbie#N (#Rlevel 1#N).";
        var result = ColorTranslator.TranslateColors(input);
        
        // Should contain ANSI codes but not #X codes
        Assert.DoesNotContain("#B", result);
        Assert.DoesNotContain("#N", result);
        Assert.DoesNotContain("#C", result);
        Assert.DoesNotContain("#R", result);
        
        // Should contain actual escape sequences
        Assert.Contains("\x1B[", result);
    }
}
