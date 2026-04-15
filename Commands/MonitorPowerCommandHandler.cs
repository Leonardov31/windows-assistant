using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles monitor power on/off voice commands via DDC/CI DPMS (VCP 0xD6).
/// Single monitor only — no "all monitors" power commands.
///
/// Form 1: {power_word} {monitor}  — "turn off monitor 1", "desligar primeiro"
/// Form 2: {monitor} {power_word}  — "first off", "primeiro desligar"
/// </summary>
public sealed class MonitorPowerCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    // Patterns built from CommandVocabulary
    private static readonly string Target = CommandVocabulary.MonitorTargetPattern();
    private static readonly string Power = CommandVocabulary.PowerWordPattern();

    // Form 1: "turn off monitor 1", "desligar primeiro"
    internal static readonly Regex PowerFirstPattern = new(
        $@"\b({Power})\s+({Target})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Form 2: "first off", "primeiro desligar"
    internal static readonly Regex TargetFirstPattern = new(
        $@"\b({Target})\s+({Power})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public MonitorPowerCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "MonitorPower";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        var monitors = CommandVocabulary.MonitorNumberChoices();
        var power = CommandVocabulary.PowerChoices(culture);
        var ordinalWords = CommandVocabulary.OrdinalWordList(culture);

        var branches = new List<GrammarBuilder>();

        // Form 1: power + "monitor N"
        var pf1Monitor = new GrammarBuilder();
        pf1Monitor.Append(power);
        pf1Monitor.Append("monitor");
        pf1Monitor.Append(monitors);
        branches.Add(pf1Monitor);

        // Form 1: power + ordinal (one branch per ordinal)
        foreach (var ordinal in ordinalWords)
        {
            var b = new GrammarBuilder();
            b.Append(power);
            b.Append(ordinal);
            branches.Add(b);
        }

        // Form 2: "monitor N" + power
        var tf1Monitor = new GrammarBuilder();
        tf1Monitor.Append("monitor");
        tf1Monitor.Append(monitors);
        tf1Monitor.Append(power);
        branches.Add(tf1Monitor);

        // Form 2: ordinal + power (one branch per ordinal)
        foreach (var ordinal in ordinalWords)
        {
            var b = new GrammarBuilder();
            b.Append(ordinal);
            b.Append(power);
            branches.Add(b);
        }

        return new Choices(branches.ToArray());
    }

    public CommandResult? TryHandle(RecognitionOutput output)
    {
        // Form 1: "turn off monitor 1", "desligar primeiro"
        var match = PowerFirstPattern.Match(output.Text);
        if (match.Success)
        {
            string powerWord = match.Groups[1].Value;
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[2].Value);
            if (index >= 0 && (CommandVocabulary.IsPowerOn(powerWord) || CommandVocabulary.IsPowerOff(powerWord)))
                return Execute(index, CommandVocabulary.IsPowerOn(powerWord));
        }

        // Form 2: "first off", "primeiro desligar"
        match = TargetFirstPattern.Match(output.Text);
        if (match.Success)
        {
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[1].Value);
            string powerWord = match.Groups[2].Value;
            if (index >= 0 && (CommandVocabulary.IsPowerOn(powerWord) || CommandVocabulary.IsPowerOff(powerWord)))
                return Execute(index, CommandVocabulary.IsPowerOn(powerWord));
        }

        return null;
    }

    private CommandResult Execute(int monitorIndex, bool turnOn)
    {
        if (turnOn)
            _monitorService.RefreshMonitors();

        bool ok = _monitorService.SetMonitorPower(monitorIndex, turnOn);
        string state = turnOn ? "on" : "standby";

        return ok
            ? new CommandResult(true, $"Monitor {monitorIndex + 1} → {state}")
            : new CommandResult(false, $"Could not set monitor {monitorIndex + 1} to {state}. Check DDC/CI support.");
    }
}
