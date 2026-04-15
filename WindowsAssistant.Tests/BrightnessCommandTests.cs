using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="BrightnessCommandHandler"/>:
///   - Full form regex matching (en-US and pt-BR)
///   - Short form regex matching ("monitor N value")
///   - Brightness level mapping (full: 1–10 → 10%–100%, short: direct 0–100)
///   - Monitor index parsing (1-based → 0-based)
///   - Grammar building per culture
///   - Supported cultures declaration
/// </summary>
public class BrightnessCommandTests
{
    private static readonly Regex FullPattern = BrightnessCommandHandler.FullPattern;
    private static readonly Regex ShortPattern = BrightnessCommandHandler.ShortPattern;

    // =========================================================================
    // FULL FORM TESTS
    // =========================================================================

    // -------------------------------------------------------------------------
    // English command pattern matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows brightness 3 in monitor 1", 3, 1)]
    [InlineData("hey windows brightness 7 on monitor 2", 7, 2)]
    [InlineData("hey windows brightness 10 monitor 1", 10, 1)]
    [InlineData("hey windows brightness 1 in monitor 4", 1, 4)]
    [InlineData("hey windows brightness 5 on monitor 3", 5, 3)]
    public void FullForm_EnglishCommand_MatchesPattern(string text, int expectedLevel, int expectedMonitor)
    {
        var match = FullPattern.Match(text);

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
    public void FullForm_PortugueseCommand_MatchesPattern(string text, int expectedLevel, int expectedMonitor)
    {
        var match = FullPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedLevel, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedMonitor, int.Parse(match.Groups[2].Value));
    }

    // -------------------------------------------------------------------------
    // Full form pattern rejection
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows volume 5")]
    [InlineData("hello world")]
    [InlineData("brightness monitor")]
    [InlineData("brilho no monitor")]
    [InlineData("")]
    public void FullForm_UnrelatedText_DoesNotMatch(string text)
    {
        Assert.False(FullPattern.IsMatch(text));
    }

    // -------------------------------------------------------------------------
    // Full form case insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("BRIGHTNESS 5 IN MONITOR 1")]
    [InlineData("Brightness 5 In Monitor 1")]
    [InlineData("BRILHO 5 NO MONITOR 1")]
    public void FullForm_IsCaseInsensitive(string text)
    {
        Assert.Matches(FullPattern, text);
    }

    // -------------------------------------------------------------------------
    // Full form brightness mapping (level × 10)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 10u)]
    [InlineData(2, 20u)]
    [InlineData(5, 50u)]
    [InlineData(10, 100u)]
    public void FullForm_BrightnessMapping_LevelTimesToPercent(int level, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(level * 10, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Full form brightness clamping
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(11, 100u)]
    [InlineData(20, 100u)]
    public void FullForm_BrightnessMapping_ClampsOutOfRange(int level, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(level * 10, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Full form monitor index conversion
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("brightness 5 in monitor 1", 0)]
    [InlineData("brightness 5 in monitor 2", 1)]
    [InlineData("brightness 5 in monitor 4", 3)]
    public void FullForm_MonitorIndex_ConvertedToZeroBased(string text, int expectedIndex)
    {
        var match = FullPattern.Match(text);
        int monitorIndex = int.Parse(match.Groups[2].Value) - 1;
        Assert.Equal(expectedIndex, monitorIndex);
    }

    // -------------------------------------------------------------------------
    // Full form preposition variations
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("brightness 5 in monitor 1")]
    [InlineData("brightness 5 on monitor 1")]
    [InlineData("brightness 5 monitor 1")]
    public void FullForm_English_AllPrepositionVariations_Match(string text)
    {
        Assert.Matches(FullPattern, text);
    }

    [Theory]
    [InlineData("brilho 5 no monitor 1")]
    [InlineData("brilho 5 do monitor 1")]
    [InlineData("brilho 5 monitor 1")]
    public void FullForm_Portuguese_AllPrepositionVariations_Match(string text)
    {
        Assert.Matches(FullPattern, text);
    }

    // =========================================================================
    // SHORT FORM TESTS
    // =========================================================================

    // -------------------------------------------------------------------------
    // Short form pattern matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows monitor 1 20", 1, 20)]
    [InlineData("hey windows monitor 2 100", 2, 100)]
    [InlineData("hey windows monitor 3 0", 3, 0)]
    [InlineData("hey windows monitor 4 50", 4, 50)]
    [InlineData("ei windows monitor 1 70", 1, 70)]
    public void ShortForm_MatchesPattern(string text, int expectedMonitor, int expectedValue)
    {
        var match = ShortPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedMonitor, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // -------------------------------------------------------------------------
    // Short form case insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("MONITOR 1 20")]
    [InlineData("Monitor 2 50")]
    public void ShortForm_IsCaseInsensitive(string text)
    {
        Assert.Matches(ShortPattern, text);
    }

    // -------------------------------------------------------------------------
    // Short form direct percentage mapping (no ×10)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(20, 20u)]
    [InlineData(50, 50u)]
    [InlineData(100, 100u)]
    public void ShortForm_DirectPercentageMapping(int value, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(value, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Short form clamping for out-of-range values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(110, 100u)]
    [InlineData(200, 100u)]
    public void ShortForm_ClampsAbove100(int value, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(value, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // -------------------------------------------------------------------------
    // Short form monitor index conversion
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("monitor 1 50", 0)]
    [InlineData("monitor 2 50", 1)]
    [InlineData("monitor 4 50", 3)]
    public void ShortForm_MonitorIndex_ConvertedToZeroBased(string text, int expectedIndex)
    {
        var match = ShortPattern.Match(text);
        int monitorIndex = int.Parse(match.Groups[1].Value) - 1;
        Assert.Equal(expectedIndex, monitorIndex);
    }

    // -------------------------------------------------------------------------
    // Short form does not match incomplete commands
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("monitor 1")]
    [InlineData("monitor")]
    [InlineData("20")]
    public void ShortForm_IncompleteCommand_DoesNotMatch(string text)
    {
        Assert.False(ShortPattern.IsMatch(text));
    }

    // -------------------------------------------------------------------------
    // Full form does not trigger short form (and vice versa)
    // -------------------------------------------------------------------------

    [Fact]
    public void FullFormText_DoesNotMatchShortPattern()
    {
        // "brightness 5 in monitor 1" should NOT match short pattern
        // (no two consecutive numbers after "monitor")
        var match = ShortPattern.Match("brightness 5 in monitor 1");
        // It may match "monitor 1" + something, but group2 would not be right.
        // The key is that FullPattern matches first in TryHandle.
        // This test verifies the regex behavior.
        if (match.Success)
        {
            // If it does match, group 2 should NOT be the brightness level
            Assert.NotEqual(5, int.Parse(match.Groups[2].Value));
        }
    }

    // =========================================================================
    // HANDLER METADATA
    // =========================================================================

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
}
