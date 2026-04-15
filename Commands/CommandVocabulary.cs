using System.Globalization;
using System.Speech.Recognition;
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
        ["monitor 1"] = 0, ["monitor 2"] = 1, ["monitor 3"] = 2, ["monitor 4"] = 3,
        // en-US ordinals
        ["first"] = 0, ["second"] = 1, ["third"] = 2,
        // pt-BR ordinals
        ["primeiro"] = 0, ["segundo"] = 1, ["terceiro"] = 2,
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
        "on", "enable", "turn on", "ligar", "ativar",
    };

    public static readonly HashSet<string> PowerOffWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "off", "disable", "turn off", "desligar", "desativar",
    };

    public static bool IsPowerOn(string word) => PowerOnWords.Contains(word);
    public static bool IsPowerOff(string word) => PowerOffWords.Contains(word);

    // -------------------------------------------------------------------------
    // Brightness keywords and prepositions per culture
    // -------------------------------------------------------------------------

    public static readonly Dictionary<string, string[]> BrightnessWords = new()
    {
        ["en-US"] = ["brightness"],
        ["pt-BR"] = ["brilho"],
    };

    public static readonly Dictionary<string, string[]> Prepositions = new()
    {
        ["en-US"] = ["on", "in"],
        ["pt-BR"] = ["no", "do", "em"],
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
    // Grammar helpers — build Choices for speech recognition per culture
    // -------------------------------------------------------------------------

    /// <summary>Ordinal word list for a given culture (for building separate grammar paths).</summary>
    public static string[] OrdinalWordList(CultureInfo culture)
    {
        return culture.Name switch
        {
            "pt-BR" => ["primeiro", "segundo", "terceiro"],
            _       => ["first", "second", "third"],
        };
    }

    /// <summary>Choices for ordinal words in a given culture.</summary>
    public static Choices OrdinalChoices(CultureInfo culture) =>
        new(OrdinalWordList(culture));

    /// <summary>Choices for "all monitors" words in a given culture.</summary>
    public static Choices AllChoices(CultureInfo culture)
    {
        return culture.Name switch
        {
            "pt-BR" => new Choices("ambos", "todos"),
            _       => new Choices("both", "all"),
        };
    }

    /// <summary>Choices for power on/off words in a given culture.</summary>
    public static Choices PowerOnChoices(CultureInfo culture)
    {
        return culture.Name switch
        {
            "pt-BR" => new Choices("ligar", "ativar"),
            _       => new Choices("on", "enable", "turn on"),
        };
    }

    public static Choices PowerOffChoices(CultureInfo culture)
    {
        return culture.Name switch
        {
            "pt-BR" => new Choices("desligar", "desativar"),
            _       => new Choices("off", "disable", "turn off"),
        };
    }

    public static Choices PowerChoices(CultureInfo culture)
    {
        var on = PowerOnChoices(culture);
        var off = PowerOffChoices(culture);
        return new Choices(new GrammarBuilder(on), new GrammarBuilder(off));
    }

    /// <summary>Choices for brightness keywords in a given culture.</summary>
    public static Choices BrightnessKeywordChoices(CultureInfo culture)
    {
        var words = BrightnessWords.GetValueOrDefault(culture.Name, ["brightness"]);
        return new Choices(words);
    }

    /// <summary>Choices for prepositions in a given culture.</summary>
    public static Choices PrepositionChoices(CultureInfo culture)
    {
        var words = Prepositions.GetValueOrDefault(culture.Name, ["on", "in"]);
        return new Choices(words);
    }

    /// <summary>Choices for monitor number (1–4).</summary>
    public static Choices MonitorNumberChoices() => new("1", "2", "3", "4");

    /// <summary>Choices for brightness values (speech recognition grammar).</summary>
    public static Choices BrightnessValueChoices() => new(
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
        "20", "30", "40", "50", "60", "70", "80", "90", "100");

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
