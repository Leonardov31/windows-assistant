using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles voice commands that change monitor brightness via DDC/CI.
///
/// English (en-US):
///   "brightness [1-10] in/on monitor [1-4]"
///
/// Portuguese (pt-BR):
///   "brilho [1-10] no/do monitor [1-4]"
///
/// The level 1–10 maps to 10%–100% brightness.
/// </summary>
public sealed class BrightnessCommandHandler : ICommandHandler
{
    private static readonly CultureInfo EnUs = new("en-US");
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly Regex CommandPattern = new(
        @"(?:brightness|brilho)\s+(\d+)(?:\s+(?:in|on|no|do))?\s+monitor\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public BrightnessCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "Brightness";

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [EnUs, PtBr];

    public GrammarBuilder BuildGrammar(CultureInfo culture)
    {
        var levels   = new Choices("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
        var monitors = new Choices("1", "2", "3", "4");

        var builder = new GrammarBuilder();

        if (culture.Name == "pt-BR")
        {
            builder.Append("brilho");
            builder.Append(levels);
            builder.Append("no", 0, 1);
            builder.Append("do", 0, 1);
            builder.Append("monitor");
            builder.Append(monitors);
        }
        else
        {
            builder.Append("brightness");
            builder.Append(levels);
            builder.Append("in", 0, 1);
            builder.Append("on", 0, 1);
            builder.Append("monitor");
            builder.Append(monitors);
        }

        return builder;
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        var match = CommandPattern.Match(result.Text);
        if (!match.Success)
            return null;

        int level        = int.Parse(match.Groups[1].Value);
        int monitorIndex = int.Parse(match.Groups[2].Value) - 1;
        uint brightness  = (uint)Math.Clamp(level * 10, 0, 100);

        bool ok = _monitorService.SetBrightness(monitorIndex, brightness);

        return ok
            ? new CommandResult(true,  $"Monitor {monitorIndex + 1} → {brightness}%")
            : new CommandResult(false, $"Could not set brightness on monitor {monitorIndex + 1}. Check DDC/CI support.");
    }
}
