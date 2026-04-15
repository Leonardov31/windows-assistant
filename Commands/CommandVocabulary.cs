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
        "on", "enable", "turn on", "power on", "wake", "wake up",
        // pt-BR (infinitive + common indicative/imperative variants)
        "ligar", "liga", "ligue", "ativar", "acender", "acende", "acenda",
        "acorda", "acorde", "acordar", "desperta", "desperte", "despertar",
    };

    public static readonly HashSet<string> PowerOffWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // en-US
        "off", "disable", "turn off", "power off", "shut down", "shut off", "sleep",
        // pt-BR
        "desligar", "desliga", "desligue", "desativar", "apagar", "apaga", "apague",
        "dormir", "dorme", "durma", "adormecer", "adormece", "adormeça",
    };

    public static bool IsPowerOn(string word) => PowerOnWords.Contains(word);
    public static bool IsPowerOff(string word) => PowerOffWords.Contains(word);

    // -------------------------------------------------------------------------
    // Brightness keywords and prepositions per culture
    // -------------------------------------------------------------------------

    public static readonly Dictionary<string, string[]> BrightnessWords = new()
    {
        ["en-US"] = ["brightness", "light"],
        ["pt-BR"] = ["brilho", "luminosidade", "luz"],
    };

    public static readonly Dictionary<string, string[]> Prepositions = new()
    {
        ["en-US"] = ["on", "in"],
        ["pt-BR"] = ["no", "na", "do", "da", "em"],
    };

    /// <summary>
    /// Action verbs that can lead a "set X to N" brightness phrasing —
    /// "set monitor one to 50", "ajusta o primeiro em 50", etc.
    /// </summary>
    public static readonly Dictionary<string, string[]> SetVerbs = new()
    {
        ["en-US"] = ["set", "put", "make", "change", "adjust", "configure"],
        ["pt-BR"] = [
            "ajustar", "ajusta", "ajuste",
            "definir", "define", "defina",
            "colocar", "coloca", "coloque",
            "mudar", "muda", "mude",
            "deixar", "deixa", "deixe",
            "pôr", "por",
        ],
    };

    /// <summary>
    /// Words that connect a target to a numeric value — "to 50", "em 50",
    /// "at 80", "para 80". Used by the verb-led brightness pattern.
    /// </summary>
    public static readonly Dictionary<string, string[]> ValueConnectors = new()
    {
        ["en-US"] = ["to", "at"],
        ["pt-BR"] = ["em", "pra", "para", "a"],
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

    /// <summary>Regex alternation for all "set N" verbs across cultures.</summary>
    internal static string SetVerbPattern()
    {
        var all = SetVerbs.Values.SelectMany(v => v).Distinct().Select(Regex.Escape);
        return $@"(?:{string.Join("|", all)})";
    }

    /// <summary>Regex alternation for value connectors ("to", "em", "para"...).</summary>
    internal static string ValueConnectorPattern()
    {
        var all = ValueConnectors.Values.SelectMany(v => v).Distinct().Select(Regex.Escape);
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

    /// <summary>
    /// Word-to-digit map used in the Vosk grammar. Vosk emits tokens from the
    /// model's pronunciation dictionary, which contains spelled-out numbers
    /// but NOT digit characters — adding "5" to the grammar would make it
    /// impossible to ever emit. After transcription we map these back to
    /// digit strings so the existing regex-based parsers (\d+) keep working.
    /// </summary>
    public static readonly Dictionary<string, string> NumberWordsEnUs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = "0", ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4",
        ["five"] = "5", ["six"] = "6", ["seven"] = "7", ["eight"] = "8", ["nine"] = "9",
        ["ten"] = "10",
        ["twenty"] = "20", ["thirty"] = "30", ["forty"] = "40", ["fifty"] = "50",
        ["sixty"]  = "60", ["seventy"] = "70", ["eighty"] = "80", ["ninety"] = "90",
        ["hundred"] = "100",
    };

    public static readonly Dictionary<string, string> NumberWordsPtBr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = "0", ["um"] = "1", ["uma"] = "1",
        ["dois"] = "2", ["duas"] = "2",
        ["três"] = "3", ["tres"] = "3",
        ["quatro"] = "4", ["cinco"] = "5", ["seis"] = "6", ["sete"] = "7",
        ["oito"] = "8", ["nove"] = "9", ["dez"] = "10",
        ["vinte"] = "20", ["trinta"] = "30", ["quarenta"] = "40",
        ["cinquenta"] = "50", ["cinqüenta"] = "50",
        ["sessenta"] = "60", ["setenta"] = "70",
        ["oitenta"] = "80", ["noventa"] = "90", ["cem"] = "100",
    };

    /// <summary>Number words emitted by the Vosk grammar for a given culture.</summary>
    public static string[] NumericWords(CultureInfo culture) =>
        culture.Name == "pt-BR"
            ? NumberWordsPtBr.Keys.ToArray()
            : NumberWordsEnUs.Keys.ToArray();

    /// <summary>
    /// Converts any number words present in <paramref name="text"/> to their
    /// digit equivalents ("brilho cinco no primeiro" → "brilho 5 no primeiro").
    /// Leaves unknown tokens untouched.
    /// </summary>
    public static string NormalizeNumbers(string text, CultureInfo culture)
    {
        var map = culture.Name == "pt-BR" ? NumberWordsPtBr : NumberWordsEnUs;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (map.TryGetValue(tokens[i], out var digit))
                tokens[i] = digit;
        }
        return string.Join(' ', tokens);
    }

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
