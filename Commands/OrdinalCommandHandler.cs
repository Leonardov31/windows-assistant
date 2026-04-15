using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles ordinal-based shortcuts for brightness and power control.
///
/// Brightness:
///   "first 2"  / "primeiro 2"   → monitor 1 at 20% (level ×10)
///   "first 50" / "primeiro 50"  → monitor 1 at 50%
///   "both 5"   / "ambos 5"      → all monitors at 50%
///
/// Power (single monitor only):
///   "first off"  / "primeiro desligar"  → monitor 1 standby
///   "second on"  / "segundo ligar"      → monitor 2 on
/// </summary>
public sealed class OrdinalCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    internal static readonly Dictionary<string, int> OrdinalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["first"] = 0, ["second"] = 1, ["third"] = 2, ["fourth"] = 3,
        ["primeiro"] = 0, ["segundo"] = 1, ["terceiro"] = 2, ["quarto"] = 3,
    };

    private const string OrdinalGroup = @"(first|second|third|fourth|primeiro|segundo|terceiro|quarto)";
    private const string AllGroup = @"(both|all|ambos|todos)";
    private const string PowerGroup = @"(on|off|ligar|desligar)";

    // Ordinal + number: "first 20", "segundo 5"
    internal static readonly Regex OrdinalBrightnessPattern = new(
        $@"\b{OrdinalGroup}\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Ordinal + power: "first off", "primeiro ligar"
    internal static readonly Regex OrdinalPowerPattern = new(
        $@"\b{OrdinalGroup}\s+{PowerGroup}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // All + number: "both 20", "ambos 5"
    internal static readonly Regex AllBrightnessPattern = new(
        $@"\b{AllGroup}\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public OrdinalCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "Ordinal";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        var values = new Choices(
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "20", "30", "40", "50", "60", "70", "80", "90", "100");

        GrammarBuilder ordinalBrightness, ordinalPower, allBrightness;

        if (culture.Name == "pt-BR")
        {
            var ordinals = new Choices("primeiro", "segundo", "terceiro", "quarto");
            var allWords = new Choices("ambos", "todos");
            var power = new Choices("ligar", "desligar");

            ordinalBrightness = new GrammarBuilder();
            ordinalBrightness.Append(ordinals);
            ordinalBrightness.Append(values);

            ordinalPower = new GrammarBuilder();
            ordinalPower.Append(ordinals);
            ordinalPower.Append(power);

            allBrightness = new GrammarBuilder();
            allBrightness.Append(allWords);
            allBrightness.Append(values);
        }
        else
        {
            var ordinals = new Choices("first", "second", "third", "fourth");
            var allWords = new Choices("both", "all");
            var power = new Choices("on", "off");

            ordinalBrightness = new GrammarBuilder();
            ordinalBrightness.Append(ordinals);
            ordinalBrightness.Append(values);

            ordinalPower = new GrammarBuilder();
            ordinalPower.Append(ordinals);
            ordinalPower.Append(power);

            allBrightness = new GrammarBuilder();
            allBrightness.Append(allWords);
            allBrightness.Append(values);
        }

        return new Choices(ordinalBrightness, ordinalPower, allBrightness);
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        // Ordinal + power: "first off"
        var match = OrdinalPowerPattern.Match(result.Text);
        if (match.Success && OrdinalMap.TryGetValue(match.Groups[1].Value, out int powerIdx))
        {
            bool turnOn = IsPowerOn(match.Groups[2].Value);
            if (turnOn) _monitorService.RefreshMonitors();
            bool ok = _monitorService.SetMonitorPower(powerIdx, turnOn);
            string state = turnOn ? "on" : "standby";
            return ok
                ? new CommandResult(true, $"Monitor {powerIdx + 1} → {state}")
                : new CommandResult(false, $"Could not set monitor {powerIdx + 1} to {state}.");
        }

        // Ordinal + brightness: "first 20"
        match = OrdinalBrightnessPattern.Match(result.Text);
        if (match.Success && OrdinalMap.TryGetValue(match.Groups[1].Value, out int brightIdx))
        {
            uint brightness = ParseBrightness(match.Groups[2].Value);
            bool ok = _monitorService.SetBrightness(brightIdx, brightness);
            return ok
                ? new CommandResult(true, $"Monitor {brightIdx + 1} → {brightness}%")
                : new CommandResult(false, $"Could not set brightness on monitor {brightIdx + 1}.");
        }

        // All + brightness: "both 50"
        match = AllBrightnessPattern.Match(result.Text);
        if (match.Success)
        {
            uint brightness = ParseBrightness(match.Groups[2].Value);
            bool ok = _monitorService.SetAllBrightness(brightness);
            return ok
                ? new CommandResult(true, $"All monitors → {brightness}%")
                : new CommandResult(false, _monitorService.Count == 0
                    ? "No DDC/CI monitors detected."
                    : "Could not set brightness on all monitors.");
        }

        return null;
    }

    private static bool IsPowerOn(string action) =>
        action.Equals("on", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("ligar", StringComparison.OrdinalIgnoreCase);

    private static uint ParseBrightness(string value)
    {
        int v = int.Parse(value);
        return (uint)Math.Clamp(v is >= 1 and <= 10 ? v * 10 : v, 0, 100);
    }
}
