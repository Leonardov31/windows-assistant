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
/// Verb-led:       {set} [{brightness} [{prep}]] {monitor} [{conn}] {value}
///                                                 — "set monitor one to 50", "ajusta primeiro em 30"
/// Number-first:   {value} {prep} {monitor}        — "50 on monitor one", "cinquenta no primeiro"
/// No target:      {brightness} [in|em] {value}    — "brilho 30", "luz 2", "brightness in 50"
///                                                 — applies to ALL monitors (same as "ambos/both")
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
    private static readonly string Verb = CommandVocabulary.SetVerbPattern();
    private static readonly string Conn = CommandVocabulary.ValueConnectorPattern();

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

    // Verb-led: "set monitor one to 50", "put first at 80",
    //           "adjust brightness on first to 50", "ajusta primeiro em 30",
    //           "deixa o primeiro em 50"
    // The brightness keyword + preposition block is optional so all three
    // shapes above share one pattern.
    internal static readonly Regex VerbLedPattern = new(
        $@"\b{Verb}\s+(?:(?:\w+\s+)?{BWord}\s+(?:{Prep}\s+)?)?({Target})\s+(?:{Conn}\s+)?(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Number-first: "50 on monitor one", "cinquenta no primeiro", "80 no segundo"
    internal static readonly Regex NumberFirstPattern = new(
        $@"\b(\d+)\s+{Prep}\s+({Target})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // No-target: "brilho 30", "brilho em 30", "luz 2", "brightness 50",
    //            "brightness in 50", "light 30"
    // Connector is restricted to "in" / "em" (the user-facing spec) rather
    // than the full preposition list — keeps this from stealing matches
    // of Long1 ("brightness 5 on monitor 1") or Long2.
    // Applies to ALL monitors.
    internal static readonly Regex NoTargetPattern = new(
        $@"\b{BWord}(?:\s+(?:in|em))?\s+(\d+)\b",
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
        foreach (var n in CommandVocabulary.NumericWords(culture))    words.Add(n);

        if (CommandVocabulary.SetVerbs.TryGetValue(culture.Name, out var setVerbs))
            foreach (var v in setVerbs) words.Add(v);

        if (CommandVocabulary.ValueConnectors.TryGetValue(culture.Name, out var connectors))
            foreach (var c in connectors) words.Add(c);

        var cultureWords = culture.Name switch
        {
            "pt-BR" => new[] { "ambos", "todos", "brilho", "luminosidade", "luz", "no", "na", "do", "da", "em" },
            _       => new[] { "both", "all", "brightness", "light", "on", "in" },
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

        // Verb-led: "set monitor one to 50", "ajusta primeiro em 30"
        // Tried before Short because Short matches a subset of this phrasing
        // ("monitor one 50") and would steal matches where the verb/connector
        // is important context.
        match = VerbLedPattern.Match(output.Text);
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

        // Number-first: "50 on monitor one", "cinquenta no primeiro"
        match = NumberFirstPattern.Match(output.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[1].Value));
            int index = CommandVocabulary.ResolveMonitorIndex(match.Groups[2].Value);
            if (index >= 0)
                return ExecuteSingle(index, brightness);
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

        // No target: "brilho 30", "luz 2", "brightness 50", "light 30"
        // Tried last — every other pattern with a target (Long1/2, VerbLed,
        // All, NumberFirst, Short) gets a chance to claim the utterance
        // before we fall back to "all monitors".
        match = NoTargetPattern.Match(output.Text);
        if (match.Success)
        {
            uint brightness = CommandVocabulary.ParseBrightness(int.Parse(match.Groups[1].Value));
            return ExecuteAll(brightness);
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
