using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles voice commands that change monitor brightness via DDC/CI.
///
/// Full form (level 1–10 → 10%–100%):
///   en-US: "brightness [1-10] in/on monitor [1-4]"
///   pt-BR: "brilho [1-10] no/do monitor [1-4]"
///
/// Short form (level 1–10 → ×10, or direct percentage 0/20–100):
///   "monitor [1-4] [1-10]"   — level: "monitor 1 2" = 20%
///   "monitor [1-4] [0-100]"  — direct: "monitor 1 50" = 50%
/// </summary>
public sealed class BrightnessCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    // Full form: "brightness 3 in monitor 1" / "brilho 3 no monitor 1"
    internal static readonly Regex FullPattern = new(
        @"(?:brightness|brilho)\s+(\d+)(?:\s+(?:in|on|no|do))?\s+monitor\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Short form: "monitor 1 20"
    internal static readonly Regex ShortPattern = new(
        @"\bmonitor\s+(\d+)\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public BrightnessCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "Brightness";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        // Full form grammar
        var levels   = new Choices("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
        var monitors = new Choices("1", "2", "3", "4");

        var fullBuilder = new GrammarBuilder();

        if (culture.Name == "pt-BR")
        {
            fullBuilder.Append("brilho");
            fullBuilder.Append(levels);
            fullBuilder.Append("no", 0, 1);
            fullBuilder.Append("do", 0, 1);
            fullBuilder.Append("monitor");
            fullBuilder.Append(monitors);
        }
        else
        {
            fullBuilder.Append("brightness");
            fullBuilder.Append(levels);
            fullBuilder.Append("in", 0, 1);
            fullBuilder.Append("on", 0, 1);
            fullBuilder.Append("monitor");
            fullBuilder.Append(monitors);
        }

        // Short form grammar: "monitor 1 20" or "monitor 1 2" (level 2 = 20%)
        var shortValues = new Choices(
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "20", "30", "40", "50", "60", "70", "80", "90", "100");
        var shortBuilder = new GrammarBuilder();
        shortBuilder.Append("monitor");
        shortBuilder.Append(monitors);
        shortBuilder.Append(shortValues);

        return new Choices(fullBuilder, shortBuilder);
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        // Try full form first: "brightness 3 in monitor 1"
        var fullMatch = FullPattern.Match(result.Text);
        if (fullMatch.Success)
        {
            int level        = int.Parse(fullMatch.Groups[1].Value);
            int monitorIndex = int.Parse(fullMatch.Groups[2].Value) - 1;
            uint brightness  = (uint)Math.Clamp(level * 10, 0, 100);

            return Execute(monitorIndex, brightness);
        }

        // Try short form: "monitor 1 20" or "monitor 1 2" (level 1-10 → ×10)
        var shortMatch = ShortPattern.Match(result.Text);
        if (shortMatch.Success)
        {
            int monitorIndex = int.Parse(shortMatch.Groups[1].Value) - 1;
            int value        = int.Parse(shortMatch.Groups[2].Value);
            uint brightness  = (uint)Math.Clamp(value is >= 1 and <= 10 ? value * 10 : value, 0, 100);

            return Execute(monitorIndex, brightness);
        }

        return null;
    }

    private CommandResult Execute(int monitorIndex, uint brightness)
    {
        bool ok = _monitorService.SetBrightness(monitorIndex, brightness);

        return ok
            ? new CommandResult(true,  $"Monitor {monitorIndex + 1} → {brightness}%")
            : new CommandResult(false, $"Could not set brightness on monitor {monitorIndex + 1}. Check DDC/CI support.");
    }
}
