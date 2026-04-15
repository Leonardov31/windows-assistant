using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles voice commands that turn a specific monitor on/off via DDC/CI DPMS (VCP 0xD6).
///
/// English (en-US):
///   "turn off monitor 1"  — standby monitor 1
///   "turn on monitor 2"   — wake monitor 2
///   "disable monitor 1"   — standby monitor 1
///   "enable monitor 2"    — wake monitor 2
///
/// Portuguese (pt-BR):
///   "desligar monitor 1"  — standby monitor 1
///   "ligar monitor 2"     — wake monitor 2
/// </summary>
public sealed class MonitorPowerCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    internal static readonly Regex ActionPattern = new(
        @"\b(turn\s+on|turn\s+off|enable|disable|ligar|desligar)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static readonly Regex TargetPattern = new(
        @"\bmonitor\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public MonitorPowerCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "MonitorPower";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        var monitors = new Choices("1", "2", "3", "4");
        var builder = new GrammarBuilder();

        if (culture.Name == "pt-BR")
        {
            builder.Append(new Choices("ligar", "desligar"));
            builder.Append("monitor");
            builder.Append(monitors);
        }
        else
        {
            builder.Append(new Choices("turn off", "turn on", "enable", "disable"));
            builder.Append("monitor");
            builder.Append(monitors);
        }

        return builder;
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        var actionMatch = ActionPattern.Match(result.Text);
        if (!actionMatch.Success)
            return null;

        var targetMatch = TargetPattern.Match(result.Text);
        if (!targetMatch.Success)
            return null;

        string action = actionMatch.Groups[1].Value.ToLowerInvariant();
        bool turnOn = action is "turn on" or "enable" or "ligar";

        int monitorIndex = int.Parse(targetMatch.Groups[1].Value) - 1;

        if (turnOn)
            _monitorService.RefreshMonitors();

        bool ok = _monitorService.SetMonitorPower(monitorIndex, turnOn);
        string state = turnOn ? "on" : "standby";

        return ok
            ? new CommandResult(true, $"Monitor {monitorIndex + 1} → {state}")
            : new CommandResult(false, $"Could not set monitor {monitorIndex + 1} to {state}. Check DDC/CI support.");
    }
}
