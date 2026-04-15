using WindowsAssistant.Commands;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="CommandVocabulary"/>:
///   - Monitor target resolution (ordinals, "monitor N")
///   - Power word classification (on/off)
///   - Brightness value parsing (0–10 → ×10, 11–100 → direct)
///   - All-target words
/// </summary>
public class CommandVocabularyTests
{
    // =========================================================================
    // MONITOR TARGET RESOLUTION
    // =========================================================================

    [Theory]
    [InlineData("monitor 1", 0)]
    [InlineData("monitor 2", 1)]
    [InlineData("monitor 3", 2)]
    [InlineData("monitor 4", 3)]
    [InlineData("first", 0)]
    [InlineData("second", 1)]
    [InlineData("third", 2)]
    [InlineData("fourth", 3)]
    [InlineData("primeiro", 0)]
    [InlineData("segundo", 1)]
    [InlineData("terceiro", 2)]
    [InlineData("quarto", 3)]
    public void ResolveMonitorIndex_ValidTarget_ReturnsIndex(string target, int expected)
    {
        Assert.Equal(expected, CommandVocabulary.ResolveMonitorIndex(target));
    }

    [Theory]
    [InlineData("FIRST", 0)]
    [InlineData("Primeiro", 0)]
    [InlineData("Monitor 1", 0)]
    public void ResolveMonitorIndex_CaseInsensitive(string target, int expected)
    {
        Assert.Equal(expected, CommandVocabulary.ResolveMonitorIndex(target));
    }

    [Theory]
    [InlineData("fifth")]
    [InlineData("monitor 5")]
    [InlineData("unknown")]
    [InlineData("")]
    public void ResolveMonitorIndex_Invalid_ReturnsNegative(string target)
    {
        Assert.Equal(-1, CommandVocabulary.ResolveMonitorIndex(target));
    }

    // =========================================================================
    // POWER WORD CLASSIFICATION
    // =========================================================================

    [Theory]
    [InlineData("on")]
    [InlineData("enable")]
    [InlineData("turn on")]
    [InlineData("ligar")]
    [InlineData("ativar")]
    public void IsPowerOn_OnWords_ReturnsTrue(string word)
    {
        Assert.True(CommandVocabulary.IsPowerOn(word));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("disable")]
    [InlineData("turn off")]
    [InlineData("desligar")]
    [InlineData("desativar")]
    public void IsPowerOff_OffWords_ReturnsTrue(string word)
    {
        Assert.True(CommandVocabulary.IsPowerOff(word));
    }

    [Theory]
    [InlineData("ON")]
    [InlineData("Ligar")]
    [InlineData("TURN ON")]
    public void IsPowerOn_CaseInsensitive(string word)
    {
        Assert.True(CommandVocabulary.IsPowerOn(word));
    }

    [Fact]
    public void PowerOnAndOff_DoNotOverlap()
    {
        foreach (var word in CommandVocabulary.PowerOnWords)
            Assert.False(CommandVocabulary.IsPowerOff(word), $"'{word}' is in both on and off");
        foreach (var word in CommandVocabulary.PowerOffWords)
            Assert.False(CommandVocabulary.IsPowerOn(word), $"'{word}' is in both on and off");
    }

    // =========================================================================
    // ALL-TARGET WORDS
    // =========================================================================

    [Theory]
    [InlineData("both")]
    [InlineData("all")]
    [InlineData("ambos")]
    [InlineData("todos")]
    [InlineData("BOTH")]
    [InlineData("Todos")]
    public void AllTargets_ContainsExpected(string word)
    {
        Assert.Contains(word, CommandVocabulary.AllTargets);
    }

    // =========================================================================
    // BRIGHTNESS PARSING
    // =========================================================================

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(1, 10u)]
    [InlineData(2, 20u)]
    [InlineData(5, 50u)]
    [InlineData(10, 100u)]
    public void ParseBrightness_LevelValues_MultipliedByTen(int value, uint expected)
    {
        Assert.Equal(expected, CommandVocabulary.ParseBrightness(value));
    }

    [Theory]
    [InlineData(11, 11u)]
    [InlineData(20, 20u)]
    [InlineData(50, 50u)]
    [InlineData(75, 75u)]
    [InlineData(100, 100u)]
    public void ParseBrightness_DirectValues_PassThrough(int value, uint expected)
    {
        Assert.Equal(expected, CommandVocabulary.ParseBrightness(value));
    }

    [Theory]
    [InlineData(110, 100u)]
    [InlineData(200, 100u)]
    [InlineData(-5, 0u)]
    public void ParseBrightness_OutOfRange_Clamped(int value, uint expected)
    {
        Assert.Equal(expected, CommandVocabulary.ParseBrightness(value));
    }
}
