using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles all brightness voice commands via DDC/CI.
///
/// Short form:     {monitor} {value}                — "first 5", "monitor 1 50", "both 3"
/// Long form 1:    {brightness} {value} {prep} {monitor} — "brightness 5 on monitor 1"
/// Long form 2:    {monitor} {brightness} {value}   — "monitor 1 brightness 5"
///
/// Values 0–10 are levels (×10). Values 11–100 are direct percentages.
/// </summary>
public sealed class BrightnessCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    // Patterns built from CommandVocabulary
    private static readonly string Target = CommandVocabulary.MonitorTargetPattern();
    private static readonly string All = CommandVocabulary.AllTargetPattern();
    private static readonly string BWord = CommandVocabulary.BrightnessWordPattern();
    private static readonly string Prep = CommandVocabulary.PrepositionPattern();

    // Short: "first 5", "monitor 1 50"
    internal static readonly Regex ShortPattern = new(
        $@"\b({Target})\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // All: "both 5", "todos 50"
    internal static readonly Regex AllPattern = new(
        $@"\b({All})\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Long 1: "brightness 5 on monitor 1", "brilho 3 no primeiro"
    internal static readonly Regex Long1Pattern = new(
        $@"\b{BWord}\s+(\d+)\s+{Prep}\s+({Target})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Long 2: "monitor 1 brightness 5", "primeiro brilho 3"
    internal static readonly Regex Long2Pattern = new(
        $@"\b({Target})\s+{BWord}\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public BrightnessCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "Brightness";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        var monitors = CommandVocabulary.MonitorNumberChoices();
        var allWords = CommandVocabulary.AllChoices(culture);
        var values = CommandVocabulary.BrightnessValueChoices();
        var bKeyword = CommandVocabulary.BrightnessKeywordChoices(culture);
        var prep = CommandVocabulary.PrepositionChoices(culture);
        var ordinalWords = CommandVocabulary.OrdinalWordList(culture);

        var branches = new List<GrammarBuilder>();

        // Short: one branch per ordinal + value (separate paths improve recognition)
        foreach (var ordinal in ordinalWords)
        {
            var b = new GrammarBuilder();
            b.Append(ordinal);
            b.Append(values);
            branches.Add(b);
        }

        // Short: "monitor N" + value
        var shortMonitor = new GrammarBuilder();
        shortMonitor.Append("monitor");
        shortMonitor.Append(monitors);
        shortMonitor.Append(values);
        branches.Add(shortMonitor);

        // All: "both/todos" + value
        var allBuilder = new GrammarBuilder();
        allBuilder.Append(allWords);
        allBuilder.Append(values);
        branches.Add(allBuilder);

        // Long 1: brightness + value + prep + "monitor N"
        var long1Monitor = new GrammarBuilder();
        long1Monitor.Append(bKeyword);
        long1Monitor.Append(values);
        long1Monitor.Append(prep);
        long1Monitor.Append("monitor");
        long1Monitor.Append(monitors);
        branches.Add(long1Monitor);

        // Long 1: brightness + value + prep + ordinal
        foreach (var ordinal in ordinalWords)
        {
            var b = new GrammarBuilder();
            b.Append(bKeyword);
            b.Append(values);
            b.Append(prep);
            b.Append(ordinal);
            branches.Add(b);
        }

        // Long 2: "monitor N" + brightness + value
        var long2Monitor = new GrammarBuilder();
        long2Monitor.Append("monitor");
        long2Monitor.Append(monitors);
        long2Monitor.Append(bKeyword);
        long2Monitor.Append(values);
        branches.Add(long2Monitor);

        // Long 2: ordinal + brightness + value
        foreach (var ordinal in ordinalWords)
        {
            var b = new GrammarBuilder();
            b.Append(ordinal);
            b.Append(bKeyword);
            b.Append(values);
            branches.Add(b);
        }

        return new Choices(branches.ToArray());
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        // Long 1: "brightness 5 on monitor 1"
        var match = Long1Pattern.Match(result.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[1].Value));
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[2].Value);
            if (index >= 0)
                return ExecuteSingle(index, brightness);
        }

        // Long 2: "monitor 1 brightness 5"
        match = Long2Pattern.Match(result.Text);
        if (match.Success)
        {
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[1].Value);
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[2].Value));
            if (index >= 0)
                return ExecuteSingle(index, brightness);
        }

        // All: "both 5", "todos 50"
        match = AllPattern.Match(result.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[2].Value));
            return ExecuteAll(brightness);
        }

        // Short: "first 5", "monitor 1 50"
        match = ShortPattern.Match(result.Text);
        if (match.Success)
        {
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[1].Value);
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[2].Value));
            if (index >= 0)
                return ExecuteSingle(index, brightness);
        }

        return null;
    }

    private CommandResult ExecuteSingle(int monitorIndex, uint brightness)
    {
        bool ok = _monitorService.SetBrightness(monitorIndex, brightness);
        return ok
            ? new CommandResult(true, $"Monitor {monitorIndex + 1} → {brightness}%")
            : new CommandResult(false, $"Could not set brightness on monitor {monitorIndex + 1}. Check DDC/CI support.");
    }

    private CommandResult ExecuteAll(uint brightness)
    {
        bool ok = _monitorService.SetAllBrightness(brightness);
        return ok
            ? new CommandResult(true, $"All monitors → {brightness}%")
            : new CommandResult(false, _monitorService.Count == 0
                ? "No DDC/CI monitors detected."
                : "Could not set brightness on all monitors.");
    }
}
