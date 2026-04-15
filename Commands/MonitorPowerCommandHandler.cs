using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles voice commands that turn monitors on/off via DDC/CI DPMS (VCP 0xD6).
///
/// English (en-US):
///   "turn off monitor 1"    — standby monitor 1
///   "turn on monitor 2"     — wake monitor 2
///   "disable monitor 1"     — standby monitor 1
///   "enable monitor 2"      — wake monitor 2
///   "turn off monitor"      — standby all monitors
///   "turn off all monitors" — standby all monitors
///
/// Portuguese (pt-BR):
///   "desligar monitor 1"       — standby monitor 1
///   "ligar monitor 2"          — wake monitor 2
///   "desligar todos os monitores" — standby all monitors
/// </summary>
public sealed class MonitorPowerCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    internal static readonly Regex ActionPattern = new(
        @"\b(turn\s+on|turn\s+off|enable|disable|ligar|desligar)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static readonly Regex TargetPattern = new(
        @"\bmonitor\s+(\d+)\b|\b(?:(?:all\s+)?(?:monitors?|monitores)|todos?\s+(?:os\s+)?(?:monitors?|monitores))\b",
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
            builder.Append("todos", 0, 1);
            builder.Append("os", 0, 1);
            builder.Append(new Choices("monitor", "monitores"));
            builder.Append(monitors, 0, 1);
        }
        else
        {
            builder.Append(new Choices("turn off", "turn on", "enable", "disable"));
            builder.Append("all", 0, 1);
            builder.Append("monitor");
            builder.Append(monitors, 0, 1);
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

        // If group 1 captured a digit → specific monitor; otherwise → all
        bool isSpecific = targetMatch.Groups[1].Success;

        if (isSpecific)
        {
            int monitorIndex = int.Parse(targetMatch.Groups[1].Value) - 1;

            if (turnOn)
                _monitorService.RefreshMonitors();

            bool ok = _monitorService.SetMonitorPower(monitorIndex, turnOn);
            string state = turnOn ? "on" : "standby";

            return ok
                ? new CommandResult(true, $"Monitor {monitorIndex + 1} → {state}")
                : new CommandResult(false, $"Could not set monitor {monitorIndex + 1} to {state}. Check DDC/CI support.");
        }
        else
        {
            if (turnOn)
                _monitorService.RefreshMonitors();

            bool ok = _monitorService.SetAllMonitorsPower(turnOn);
            string state = turnOn ? "on" : "standby";
            int count = _monitorService.Count;

            return ok
                ? new CommandResult(true, $"All {count} monitor(s) → {state}")
                : new CommandResult(false, count == 0
                    ? "No DDC/CI monitors detected."
                    : $"Could not set all monitors to {state}. Some may not support DDC/CI.");
        }
    }
}
