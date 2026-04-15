using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="OrdinalCommandHandler"/>:
///   - Ordinal brightness matching (en-US/pt-BR)
///   - Ordinal power matching (on/off, ligar/desligar)
///   - All/both brightness and power matching
///   - Ordinal → monitor index mapping
///   - Brightness value logic (1-10 → ×10)
///   - Grammar building, handler metadata
/// </summary>
public class OrdinalCommandTests
{
    private static readonly Regex OrdinalBrightness = OrdinalCommandHandler.OrdinalBrightnessPattern;
    private static readonly Regex OrdinalPower = OrdinalCommandHandler.OrdinalPowerPattern;
    private static readonly Regex AllBrightness = OrdinalCommandHandler.AllBrightnessPattern;
    private static readonly Regex AllPower = OrdinalCommandHandler.AllPowerPattern;

    // =========================================================================
    // ORDINAL → INDEX MAPPING
    // =========================================================================

    [Theory]
    [InlineData("first", 0)]
    [InlineData("second", 1)]
    [InlineData("third", 2)]
    [InlineData("fourth", 3)]
    [InlineData("primeiro", 0)]
    [InlineData("segundo", 1)]
    [InlineData("terceiro", 2)]
    [InlineData("quarto", 3)]
    public void OrdinalMap_MapsCorrectly(string ordinal, int expectedIndex)
    {
        Assert.True(OrdinalCommandHandler.OrdinalMap.TryGetValue(ordinal, out int index));
        Assert.Equal(expectedIndex, index);
    }

    [Fact]
    public void OrdinalMap_IsCaseInsensitive()
    {
        Assert.True(OrdinalCommandHandler.OrdinalMap.ContainsKey("FIRST"));
        Assert.True(OrdinalCommandHandler.OrdinalMap.ContainsKey("Primeiro"));
    }

    // =========================================================================
    // ORDINAL + BRIGHTNESS
    // =========================================================================

    [Theory]
    [InlineData("hey windows first 20", "first", 20)]
    [InlineData("hey windows second 5", "second", 5)]
    [InlineData("hey windows third 100", "third", 100)]
    [InlineData("hey windows fourth 0", "fourth", 0)]
    [InlineData("ei windows primeiro 2", "primeiro", 2)]
    [InlineData("ei windows segundo 50", "segundo", 50)]
    [InlineData("ei windows terceiro 10", "terceiro", 10)]
    [InlineData("ei windows quarto 80", "quarto", 80)]
    public void OrdinalBrightness_MatchesCorrectly(string text, string expectedOrdinal, int expectedValue)
    {
        var match = OrdinalBrightness.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedOrdinal, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // =========================================================================
    // ORDINAL + POWER
    // =========================================================================

    [Theory]
    [InlineData("hey windows first off", "first", "off")]
    [InlineData("hey windows second on", "second", "on")]
    [InlineData("ei windows primeiro desligar", "primeiro", "desligar")]
    [InlineData("ei windows segundo ligar", "segundo", "ligar")]
    [InlineData("hey windows third off", "third", "off")]
    [InlineData("hey windows fourth on", "fourth", "on")]
    public void OrdinalPower_MatchesCorrectly(string text, string expectedOrdinal, string expectedAction)
    {
        var match = OrdinalPower.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedOrdinal, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedAction, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // ALL/BOTH + BRIGHTNESS
    // =========================================================================

    [Theory]
    [InlineData("hey windows both 50", "both", 50)]
    [InlineData("hey windows all 5", "all", 5)]
    [InlineData("ei windows ambos 20", "ambos", 20)]
    [InlineData("ei windows todos 3", "todos", 3)]
    public void AllBrightness_MatchesCorrectly(string text, string expectedAll, int expectedValue)
    {
        var match = AllBrightness.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedAll, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedValue, int.Parse(match.Groups[2].Value));
    }

    // =========================================================================
    // ALL/BOTH + POWER
    // =========================================================================

    [Theory]
    [InlineData("hey windows both off", "both", "off")]
    [InlineData("hey windows both on", "both", "on")]
    [InlineData("hey windows all off", "all", "off")]
    [InlineData("ei windows ambos desligar", "ambos", "desligar")]
    [InlineData("ei windows todos ligar", "todos", "ligar")]
    public void AllPower_MatchesCorrectly(string text, string expectedAll, string expectedAction)
    {
        var match = AllPower.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedAll, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedAction, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // POWER ACTION MAPPING
    // =========================================================================

    [Theory]
    [InlineData("on", true)]
    [InlineData("ligar", true)]
    [InlineData("off", false)]
    [InlineData("desligar", false)]
    public void PowerAction_MapsCorrectly(string action, bool expectedOn)
    {
        bool turnOn = action.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                      action.Equals("ligar", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedOn, turnOn);
    }

    // =========================================================================
    // BRIGHTNESS VALUE LOGIC (1-10 → ×10)
    // =========================================================================

    [Theory]
    [InlineData(1, 10u)]
    [InlineData(5, 50u)]
    [InlineData(10, 100u)]
    [InlineData(0, 0u)]
    [InlineData(20, 20u)]
    [InlineData(50, 50u)]
    [InlineData(100, 100u)]
    [InlineData(110, 100u)]
    public void BrightnessValue_MappedCorrectly(int value, uint expectedBrightness)
    {
        uint brightness = (uint)Math.Clamp(value is >= 1 and <= 10 ? value * 10 : value, 0, 100);
        Assert.Equal(expectedBrightness, brightness);
    }

    // =========================================================================
    // CASE INSENSITIVITY
    // =========================================================================

    [Theory]
    [InlineData("FIRST 20")]
    [InlineData("First Off")]
    [InlineData("BOTH 50")]
    [InlineData("PRIMEIRO 5")]
    public void Patterns_AreCaseInsensitive(string text)
    {
        bool matches = OrdinalBrightness.IsMatch(text) ||
                       OrdinalPower.IsMatch(text) ||
                       AllBrightness.IsMatch(text);
        Assert.True(matches);
    }

    // =========================================================================
    // PATTERN REJECTION
    // =========================================================================

    [Theory]
    [InlineData("brightness 5 monitor 1")]
    [InlineData("turn off monitor 1")]
    [InlineData("hello world")]
    [InlineData("")]
    public void UnrelatedText_DoesNotMatch(string text)
    {
        Assert.False(OrdinalBrightness.IsMatch(text));
        Assert.False(OrdinalPower.IsMatch(text));
        Assert.False(AllBrightness.IsMatch(text));
        Assert.False(AllPower.IsMatch(text));
    }

    // =========================================================================
    // HANDLER METADATA
    // =========================================================================

    [Fact]
    public void Handler_NameIsOrdinal()
    {
        var handler = new OrdinalCommandHandler(new MonitorControlService());
        Assert.Equal("Ordinal", handler.Name);
    }

    [Fact]
    public void Handler_SupportsBothCultures()
    {
        var handler = new OrdinalCommandHandler(new MonitorControlService());
        var names = handler.SupportedCultures.Select(c => c.Name).ToList();
        Assert.Contains("en-US", names);
        Assert.Contains("pt-BR", names);
        Assert.Equal(2, handler.SupportedCultures.Count);
    }

    [Fact]
    public void BuildGrammar_EnUs_DoesNotThrow()
    {
        var handler = new OrdinalCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildGrammar(new CultureInfo("en-US")));
    }

    [Fact]
    public void BuildGrammar_PtBr_DoesNotThrow()
    {
        var handler = new OrdinalCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildGrammar(new CultureInfo("pt-BR")));
    }
}
