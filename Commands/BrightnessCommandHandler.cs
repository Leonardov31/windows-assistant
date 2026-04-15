using System.Globalization;
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

    public IReadOnlyList<string> BuildVocabulary(CultureInfo culture)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "monitor" };

        foreach (var o in CommandVocabulary.OrdinalWordList(culture)) words.Add(o);
        foreach (var n in CommandVocabulary.NumericWords())            words.Add(n);

        var cultureWords = culture.Name switch
        {
            "pt-BR" => new[] { "ambos", "todos", "brilho", "luminosidade", "luz", "no", "na", "do", "da", "em" },
            _       => new[] { "both", "all", "brightness", "on", "in" },
        };
        foreach (var w in cultureWords) words.Add(w);

        return words.ToArray();
    }

    public CommandResult? TryHandle(RecognitionOutput output)
    {
        // Long 1: "brightness 5 on monitor 1"
        var match = Long1Pattern.Match(output.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[1].Value));
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[2].Value);
            if (index >= 0)
                return ExecuteSingle(index, brightness);
        }

        // Long 2: "monitor 1 brightness 5"
        match = Long2Pattern.Match(output.Text);
        if (match.Success)
        {
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[1].Value);
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[2].Value));
            if (index >= 0)
                return ExecuteSingle(index, brightness);
        }

        // All: "both 5", "todos 50"
        match = AllPattern.Match(output.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[2].Value));
            return ExecuteAll(brightness);
        }

        // Short: "first 5", "monitor 1 50"
        match = ShortPattern.Match(output.Text);
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
