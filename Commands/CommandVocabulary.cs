using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowsAssistant.Commands;

/// <summary>
/// Central source of truth for all voice command vocabulary.
/// Add new words, ordinals, or languages here — handlers pick them up automatically.
/// </summary>
public static class CommandVocabulary
{
    // -------------------------------------------------------------------------
    // Monitor targets — maps words to 0-based monitor index
    // -------------------------------------------------------------------------

    public static readonly Dictionary<string, int> MonitorTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Numeric (both languages)
        ["monitor 1"] = 0, ["monitor 2"] = 1, ["monitor 3"] = 2, ["monitor 4"] = 3, ["monitor 5"] = 4,
        // en-US ordinals
        ["first"] = 0, ["second"] = 1, ["third"] = 2, ["fourth"] = 3, ["fifth"] = 4,
        // pt-BR ordinals
        ["primeiro"] = 0, ["segundo"] = 1, ["terceiro"] = 2, ["quarto"] = 3, ["quinto"] = 4,
    };

    // "All monitors" words — brightness only
    public static readonly HashSet<string> AllTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "both", "all", "ambos", "todos",
    };

    // -------------------------------------------------------------------------
    // Power state words
    // -------------------------------------------------------------------------

    public static readonly HashSet<string> PowerOnWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // en-US
        "on", "enable", "turn on",
        // pt-BR (infinitive + common indicative/imperative variants)
        "ligar", "liga", "ligue", "ativar", "acender", "acende", "acenda",
    };

    public static readonly HashSet<string> PowerOffWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // en-US
        "off", "disable", "turn off",
        // pt-BR
        "desligar", "desliga", "desligue", "desativar", "apagar", "apaga", "apague",
    };

    public static bool IsPowerOn(string word) => PowerOnWords.Contains(word);
    public static bool IsPowerOff(string word) => PowerOffWords.Contains(word);

    // -------------------------------------------------------------------------
    // Brightness keywords and prepositions per culture
    // -------------------------------------------------------------------------

    public static readonly Dictionary<string, string[]> BrightnessWords = new()
    {
        ["en-US"] = ["brightness"],
        ["pt-BR"] = ["brilho", "luminosidade", "luz"],
    };

    public static readonly Dictionary<string, string[]> Prepositions = new()
    {
        ["en-US"] = ["on", "in"],
        ["pt-BR"] = ["no", "na", "do", "da", "em"],
    };

    // -------------------------------------------------------------------------
    // Brightness value parsing: 0–10 → ×10, 11–100 → direct
    // -------------------------------------------------------------------------

    public static uint ParseBrightness(int value) =>
        (uint)Math.Clamp(value is >= 0 and <= 10 ? value * 10 : value, 0, 100);

    // -------------------------------------------------------------------------
    // Regex helpers — build patterns from vocabulary
    // -------------------------------------------------------------------------

    /// <summary>Regex alternation for single-monitor targets (ordinals + "monitor N").</summary>
    internal static string MonitorTargetPattern()
    {
        var ordinals = MonitorTargets.Keys
            .Where(k => !k.StartsWith("monitor", StringComparison.OrdinalIgnoreCase))
            .Select(Regex.Escape);
        // "monitor \d+" covers all numeric forms
        return $@"(?:monitor\s+\d+|{string.Join("|", ordinals)})";
    }

    /// <summary>Regex alternation for "all monitors" words.</summary>
    internal static string AllTargetPattern() =>
        $@"(?:{string.Join("|", AllTargets.Select(Regex.Escape))})";

    /// <summary>Regex alternation for all power words (on + off).</summary>
    internal static string PowerWordPattern()
    {
        var all = PowerOnWords.Concat(PowerOffWords).Select(Regex.Escape);
        return $@"(?:{string.Join("|", all)})";
    }

    /// <summary>Regex alternation for all brightness keywords across cultures.</summary>
    internal static string BrightnessWordPattern()
    {
        var all = BrightnessWords.Values.SelectMany(v => v).Distinct().Select(Regex.Escape);
        return $@"(?:{string.Join("|", all)})";
    }

    /// <summary>Regex alternation for all prepositions across cultures.</summary>
    internal static string PrepositionPattern()
    {
        var all = Prepositions.Values.SelectMany(v => v).Distinct().Select(Regex.Escape);
        return $@"(?:{string.Join("|", all)})";
    }

    // -------------------------------------------------------------------------
    // Vocabulary helpers — word lists per culture (consumed by Vosk grammars)
    // -------------------------------------------------------------------------

    /// <summary>Ordinal words for a given culture.</summary>
    public static string[] OrdinalWordList(CultureInfo culture)
    {
        return culture.Name switch
        {
            "pt-BR" => ["primeiro", "segundo", "terceiro", "quarto", "quinto"],
            _       => ["first", "second", "third", "fourth", "fifth"],
        };
    }

    /// <summary>Flat list of numeric words (monitor numbers + brightness values).</summary>
    public static string[] NumericWords() =>
    [
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
        "20", "30", "40", "50", "60", "70", "80", "90", "100",
    ];

    // -------------------------------------------------------------------------
    // Target resolution — resolves recognized text to a monitor index
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tries to resolve a target word to a 0-based monitor index.
    /// Returns -1 if not found.
    /// </summary>
    public static int ResolveMonitorIndex(string target)
    {
        if (MonitorTargets.TryGetValue(target, out int index))
            return index;
        return -1;
    }
}
