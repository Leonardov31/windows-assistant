using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="BrightnessCommandHandler"/>:
///   - Regex pattern matching for en-US and pt-BR commands
///   - Brightness level mapping (1–10 → 10%–100%)
///   - Monitor index parsing (1-based → 0-based)
///   - Grammar building per culture
///   - Supported cultures declaration
/// </summary>
public class BrightnessCommandTests
{
    // Mirror of the private regex in BrightnessCommandHandler
    private static readonly Regex CommandPattern = new(
        @"(?:brightness|brilho)\s+(\d+)(?:\s+(?:in|on|no|do))?\s+monitor\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // English command pattern matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows brightness 3 in monitor 1", 3, 1)]
    [InlineData("hey windows brightness 7 on monitor 2", 7, 2)]
    [InlineData("hey windows brightness 10 monitor 1", 10, 1)]
    [InlineData("hey windows brightness 1 in monitor 4", 1, 4)]
    [InlineData("hey windows brightness 5 on monitor 3", 5, 3)]
    public void EnglishCommand_MatchesPattern(string text, int expectedLevel, int expectedMonitor)
    {
        var match = CommandPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedLevel, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedMonitor, int.Parse(match.Groups[2].Value));
    }

    // -------------------------------------------------------------------------
    // Portuguese command pattern matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("ei windows brilho 3 no monitor 1", 3, 1)]
    [InlineData("ei windows brilho 5 do monitor 2", 5, 2)]
    [InlineData("ei windows brilho 8 monitor 1", 8, 1)]
    [InlineData("ei windows brilho 10 no monitor 3", 10, 3)]
    public void PortugueseCommand_MatchesPattern(string text, int expectedLevel, int expectedMonitor)
    {
        var match = CommandPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedLevel, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedMonitor, int.Parse(match.Groups[2].Value));
    }

    // -------------------------------------------------------------------------
    // Pattern rejection — unrecognized commands
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows volume 5")]
    [InlineData("hello world")]
    [InlineData("brightness monitor")]
    [InlineData("brilho no monitor")]
    [InlineData("")]
    public void UnrelatedText_DoesNotMatch(string text)
    {
        var match = CommandPattern.Match(text);
        Assert.False(match.Success);
    }

    // -------------------------------------------------------------------------
    // Case insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("BRIGHTNESS 5 IN MONITOR 1")]
    [InlineData("Brightness 5 In Monitor 1")]
    [InlineData("BRILHO 5 NO MONITOR 1")]
    public void Pattern_IsCaseInsensitive(string text)
    {
        var match = CommandPattern.Match(text);
        Assert.True(match.Success);
    }

    // -------------------------------------------------------------------------
    // Brightness level mapping (level × 10 = percentage)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 10u)]
    [InlineData(2, 20u)]
    [InlineData(5, 50u)]
    [InlineData(10, 100u)]
    public void BrightnessMapping_LevelTimesToPercent(int level, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(level * 10, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Brightness clamping for out-of-range levels
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(11, 100u)]
    [InlineData(20, 100u)]
    public void BrightnessMapping_ClampsOutOfRange(int level, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(level * 10, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Monitor index conversion (1-based voice → 0-based internal)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("brightness 5 in monitor 1", 0)]
    [InlineData("brightness 5 in monitor 2", 1)]
    [InlineData("brightness 5 in monitor 4", 3)]
    public void MonitorIndex_ConvertedToZeroBased(string text, int expectedIndex)
    {
        var match = CommandPattern.Match(text);
        int monitorIndex = int.Parse(match.Groups[2].Value) - 1;
        Assert.Equal(expectedIndex, monitorIndex);
    }

    // -------------------------------------------------------------------------
    // Handler metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Handler_NameIsBrightness()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        Assert.Equal("Brightness", handler.Name);
    }

    [Fact]
    public void Handler_SupportsBothCultures()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        var cultureNames = handler.SupportedCultures.Select(c => c.Name).ToList();

        Assert.Contains("en-US", cultureNames);
        Assert.Contains("pt-BR", cultureNames);
        Assert.Equal(2, handler.SupportedCultures.Count);
    }

    // -------------------------------------------------------------------------
    // Grammar building — should not throw
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildGrammar_EnUs_DoesNotThrow()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        var grammar = handler.BuildGrammar(new CultureInfo("en-US"));
        Assert.NotNull(grammar);
    }

    [Fact]
    public void BuildGrammar_PtBr_DoesNotThrow()
    {
        var handler = new BrightnessCommandHandler(new MonitorControlService());
        var grammar = handler.BuildGrammar(new CultureInfo("pt-BR"));
        Assert.NotNull(grammar);
    }

    // -------------------------------------------------------------------------
    // Preposition variations per language
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("brightness 5 in monitor 1")]
    [InlineData("brightness 5 on monitor 1")]
    [InlineData("brightness 5 monitor 1")]
    public void English_AllPrepositionVariations_Match(string text)
    {
        Assert.Matches(CommandPattern, text);
    }

    [Theory]
    [InlineData("brilho 5 no monitor 1")]
    [InlineData("brilho 5 do monitor 1")]
    [InlineData("brilho 5 monitor 1")]
    public void Portuguese_AllPrepositionVariations_Match(string text)
    {
        Assert.Matches(CommandPattern, text);
    }
}
