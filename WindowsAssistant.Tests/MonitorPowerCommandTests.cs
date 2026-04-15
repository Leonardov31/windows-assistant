using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="MonitorPowerCommandHandler"/>:
///   - Form 1: "{power} {monitor}" — "turn off monitor 1", "desligar primeiro"
///   - Form 2: "{monitor} {power}" — "first off", "primeiro desligar"
///   - All power words (on/enable/ligar/ativar, off/disable/desligar/desativar)
///   - Grammar building, handler metadata
/// </summary>
public class MonitorPowerCommandTests
{
    private static readonly Regex PowerFirst = MonitorPowerCommandHandler.PowerFirstPattern;
    private static readonly Regex TargetFirst = MonitorPowerCommandHandler.TargetFirstPattern;

    // =========================================================================
    // FORM 1: "{power} {monitor}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows turn off monitor 1", "turn off", "monitor 1")]
    [InlineData("hey windows turn on monitor 2", "turn on", "monitor 2")]
    [InlineData("hey windows enable first", "enable", "first")]
    [InlineData("hey windows disable second", "disable", "second")]
    [InlineData("ei windows desligar monitor 1", "desligar", "monitor 1")]
    [InlineData("ei windows ligar monitor 2", "ligar", "monitor 2")]
    [InlineData("ei windows ativar primeiro", "ativar", "primeiro")]
    [InlineData("ei windows desativar segundo", "desativar", "segundo")]
    public void PowerFirst_MatchesCorrectly(string text, string expectedPower, string expectedTarget)
    {
        var match = PowerFirst.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedPower, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedTarget, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // FORM 2: "{monitor} {power}"
    // =========================================================================

    [Theory]
    [InlineData("hey windows first off", "first", "off")]
    [InlineData("hey windows second on", "second", "on")]
    [InlineData("hey windows monitor 1 off", "monitor 1", "off")]
    [InlineData("hey windows monitor 2 enable", "monitor 2", "enable")]
    [InlineData("ei windows primeiro desligar", "primeiro", "desligar")]
    [InlineData("ei windows segundo ligar", "segundo", "ligar")]
    [InlineData("ei windows terceiro desativar", "terceiro", "desativar")]
    [InlineData("ei windows terceiro ativar", "terceiro", "ativar")]
    public void TargetFirst_MatchesCorrectly(string text, string expectedTarget, string expectedPower)
    {
        var match = TargetFirst.Match(text);
        Assert.True(match.Success);
        Assert.Equal(expectedTarget, match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedPower, match.Groups[2].Value, StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // CASE INSENSITIVITY
    // =========================================================================

    [Theory]
    [InlineData("TURN OFF MONITOR 1")]
    [InlineData("DESLIGAR PRIMEIRO")]
    public void PowerFirst_CaseInsensitive(string text)
    {
        Assert.Matches(PowerFirst, text);
    }

    [Theory]
    [InlineData("FIRST OFF")]
    [InlineData("PRIMEIRO DESLIGAR")]
    public void TargetFirst_CaseInsensitive(string text)
    {
        Assert.Matches(TargetFirst, text);
    }

    // =========================================================================
    // PATTERN REJECTION
    // =========================================================================

    [Theory]
    [InlineData("brightness 5 monitor 1")]
    [InlineData("hello world")]
    [InlineData("")]
    public void UnrelatedText_DoesNotMatch(string text)
    {
        Assert.False(PowerFirst.IsMatch(text));
        Assert.False(TargetFirst.IsMatch(text));
    }

    // =========================================================================
    // NO "ALL MONITORS" POWER — patterns must NOT match
    // =========================================================================

    [Theory]
    [InlineData("turn off both")]
    [InlineData("both off")]
    [InlineData("desligar todos")]
    [InlineData("todos desligar")]
    public void AllMonitorsPower_NotSupported(string text)
    {
        // "both"/"todos" are not in MonitorTargets, so even if regex matches,
        // ResolveMonitorIndex returns -1 and handler returns null
        if (PowerFirst.IsMatch(text))
        {
            var m = PowerFirst.Match(text);
            Assert.Equal(-1, CommandVocabulary.ResolveMonitorIndex(m.Groups[2].Value));
        }
        if (TargetFirst.IsMatch(text))
        {
            var m = TargetFirst.Match(text);
            Assert.Equal(-1, CommandVocabulary.ResolveMonitorIndex(m.Groups[1].Value));
        }
    }

    // =========================================================================
    // HANDLER METADATA
    // =========================================================================

    [Fact]
    public void Handler_NameIsMonitorPower()
    {
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        Assert.Equal("MonitorPower", handler.Name);
    }

    [Fact]
    public void Handler_SupportsBothCultures()
    {
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        var names = handler.SupportedCultures.Select(c => c.Name).ToList();
        Assert.Contains("en-US", names);
        Assert.Contains("pt-BR", names);
        Assert.Equal(2, handler.SupportedCultures.Count);
    }

    [Fact]
    public void BuildGrammar_EnUs_DoesNotThrow()
    {
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildGrammar(new CultureInfo("en-US")));
    }

    [Fact]
    public void BuildGrammar_PtBr_DoesNotThrow()
    {
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        Assert.NotNull(handler.BuildGrammar(new CultureInfo("pt-BR")));
    }
}
