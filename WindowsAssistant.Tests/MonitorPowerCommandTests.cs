using System.Globalization;
using System.Text.RegularExpressions;
using WindowsAssistant.Commands;
using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="MonitorPowerCommandHandler"/>:
///   - Action regex matching (turn on/off, enable/disable, ligar/desligar)
///   - Target regex matching (specific monitor vs all)
///   - Monitor index parsing (1-based → 0-based)
///   - Grammar building per culture
///   - Handler metadata
/// </summary>
public class MonitorPowerCommandTests
{
    private static readonly Regex ActionPattern = MonitorPowerCommandHandler.ActionPattern;
    private static readonly Regex TargetPattern = MonitorPowerCommandHandler.TargetPattern;

    // -------------------------------------------------------------------------
    // English action matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows turn off monitor 1", "turn off")]
    [InlineData("hey windows turn on monitor 2", "turn on")]
    [InlineData("hey windows enable monitor 1", "enable")]
    [InlineData("hey windows disable monitor 3", "disable")]
    public void EnglishAction_MatchesCorrectly(string text, string expectedAction)
    {
        var match = ActionPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedAction, match.Groups[1].Value.ToLowerInvariant());
    }

    // -------------------------------------------------------------------------
    // Portuguese action matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("ei windows desligar monitor 1", "desligar")]
    [InlineData("ei windows ligar monitor 2", "ligar")]
    public void PortugueseAction_MatchesCorrectly(string text, string expectedAction)
    {
        var match = ActionPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(expectedAction, match.Groups[1].Value.ToLowerInvariant());
    }

    // -------------------------------------------------------------------------
    // Action → on/off mapping
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("turn on", true)]
    [InlineData("enable", true)]
    [InlineData("ligar", true)]
    [InlineData("turn off", false)]
    [InlineData("disable", false)]
    [InlineData("desligar", false)]
    public void ActionToOnOff_MapsCorrectly(string action, bool expectedOn)
    {
        bool turnOn = action is "turn on" or "enable" or "ligar";
        Assert.Equal(expectedOn, turnOn);
    }

    // -------------------------------------------------------------------------
    // Target matching — specific monitor
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("turn off monitor 1", true, 1)]
    [InlineData("turn on monitor 2", true, 2)]
    [InlineData("disable monitor 4", true, 4)]
    [InlineData("desligar monitor 3", true, 3)]
    public void TargetPattern_SpecificMonitor_CapturesIndex(string text, bool isSpecific, int expectedMonitor)
    {
        var match = TargetPattern.Match(text);

        Assert.True(match.Success);
        Assert.Equal(isSpecific, match.Groups[1].Success);
        Assert.Equal(expectedMonitor, int.Parse(match.Groups[1].Value));
    }

    // -------------------------------------------------------------------------
    // Target matching — all monitors
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("turn off all monitors")]
    [InlineData("turn off monitor")]
    [InlineData("turn on monitors")]
    [InlineData("desligar todos monitor")]
    [InlineData("desligar todos os monitors")]
    public void TargetPattern_AllMonitors_NoIndexCaptured(string text)
    {
        var match = TargetPattern.Match(text);

        Assert.True(match.Success);
        Assert.False(match.Groups[1].Success);
    }

    // -------------------------------------------------------------------------
    // Pattern rejection
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows brightness 5 monitor 1")]
    [InlineData("hello world")]
    [InlineData("")]
    public void UnrelatedText_ActionDoesNotMatch(string text)
    {
        Assert.False(ActionPattern.IsMatch(text));
    }

    // -------------------------------------------------------------------------
    // Case insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("TURN OFF MONITOR 1")]
    [InlineData("Turn On Monitor 2")]
    [InlineData("DISABLE MONITOR 3")]
    [InlineData("DESLIGAR MONITOR 1")]
    public void Patterns_AreCaseInsensitive(string text)
    {
        Assert.Matches(ActionPattern, text);
        Assert.Matches(TargetPattern, text);
    }

    // -------------------------------------------------------------------------
    // Monitor index conversion (1-based → 0-based)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("turn off monitor 1", 0)]
    [InlineData("turn off monitor 2", 1)]
    [InlineData("turn off monitor 4", 3)]
    public void MonitorIndex_ConvertedToZeroBased(string text, int expectedIndex)
    {
        var match = TargetPattern.Match(text);
        int monitorIndex = int.Parse(match.Groups[1].Value) - 1;
        Assert.Equal(expectedIndex, monitorIndex);
    }

    // -------------------------------------------------------------------------
    // Handler metadata
    // -------------------------------------------------------------------------

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
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        var grammar = handler.BuildGrammar(new CultureInfo("en-US"));
        Assert.NotNull(grammar);
    }

    [Fact]
    public void BuildGrammar_PtBr_DoesNotThrow()
    {
        var handler = new MonitorPowerCommandHandler(new MonitorControlService());
        var grammar = handler.BuildGrammar(new CultureInfo("pt-BR"));
        Assert.NotNull(grammar);
    }

    // -------------------------------------------------------------------------
    // Full command parsing (action + target combined)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("hey windows turn off monitor 1", false, true, 1)]
    [InlineData("hey windows turn on monitor 2", true, true, 2)]
    [InlineData("hey windows turn off all monitors", false, false, -1)]
    [InlineData("hey windows turn on monitor", true, false, -1)]
    [InlineData("ei windows desligar monitor 1", false, true, 1)]
    [InlineData("ei windows ligar monitor 2", true, true, 2)]
    [InlineData("ei windows desligar todos monitor", false, false, -1)]
    public void FullCommand_ParsesActionAndTarget(string text, bool expectedOn, bool expectedSpecific, int expectedMonitor)
    {
        var actionMatch = ActionPattern.Match(text);
        Assert.True(actionMatch.Success);

        string action = actionMatch.Groups[1].Value.ToLowerInvariant();
        bool turnOn = action is "turn on" or "enable" or "ligar";
        Assert.Equal(expectedOn, turnOn);

        var targetMatch = TargetPattern.Match(text);
        Assert.True(targetMatch.Success);
        Assert.Equal(expectedSpecific, targetMatch.Groups[1].Success);

        if (expectedSpecific)
            Assert.Equal(expectedMonitor, int.Parse(targetMatch.Groups[1].Value));
    }
}
