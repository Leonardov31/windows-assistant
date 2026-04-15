using System.Speech.Recognition;
using System.Text.RegularExpressions;
using WindowsAssistant.Services;

namespace WindowsAssistant.Commands;

/// <summary>
/// Handles voice commands that change monitor brightness via DDC/CI.
///
/// Supported phrases (after the wake word "Hey Windows"):
///   "brightness [1-10] in monitor [1-4]"
///   "brightness [1-10] on monitor [1-4]"
///   "brightness [1-10] monitor [1-4]"
///
/// The level 1–10 maps to 10%–100% brightness.
/// </summary>
public sealed class BrightnessCommandHandler : ICommandHandler
{
    private static readonly Regex CommandPattern = new(
        @"brightness (\d+)(?:\s+(?:in|on))?\s+monitor (\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MonitorControlService _monitorService;

    public BrightnessCommandHandler(MonitorControlService monitorService)
        => _monitorService = monitorService;

    public string Name => "Brightness";

    public GrammarBuilder BuildGrammar()
    {
        var levels   = new Choices("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
        var monitors = new Choices("1", "2", "3", "4");

        var builder = new GrammarBuilder();
        builder.Append("brightness");
        builder.Append(levels);
        builder.Append("in",  0, 1); // optional preposition
        builder.Append("on",  0, 1);
        builder.Append("monitor");
        builder.Append(monitors);

        return builder;
    }

    public CommandResult? TryHandle(RecognitionResult result)
    {
        var match = CommandPattern.Match(result.Text);
        if (!match.Success)
            return null;

        int level        = int.Parse(match.Groups[1].Value);
        int monitorIndex = int.Parse(match.Groups[2].Value) - 1; // convert to 0-based
        uint brightness  = (uint)Math.Clamp(level * 10, 0, 100);

        bool ok = _monitorService.SetBrightness(monitorIndex, brightness);

        return ok
            ? new CommandResult(true,  $"Monitor {monitorIndex + 1} brightness → {brightness}%")
            : new CommandResult(false, $"Could not set brightness on monitor {monitorIndex + 1}. " +
                                       "Check DDC/CI support or monitor index.");
    }
}
