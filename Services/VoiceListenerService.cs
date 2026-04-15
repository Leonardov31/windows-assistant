using System.Globalization;
using System.Speech.Recognition;
using WindowsAssistant.Commands;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase "Hey Windows" followed by a registered command.
/// Dispatches each match to the appropriate <see cref="ICommandHandler"/>.
///
/// Automatically adapts recognition timeouts based on observed speaking pace
/// using a rolling average of recent utterance durations.
/// </summary>
public sealed class VoiceListenerService : IDisposable
{
    private const string WakePhrase = "hey windows";
    private const int SampleWindow  = 8;

    private readonly SpeechRecognitionEngine _engine;
    private readonly IReadOnlyList<ICommandHandler> _handlers;
    private readonly Queue<double> _wordRateSamples = new();
    private SpeechSpeed _currentSpeed = SpeechSpeed.Normal;
    private float _minConfidence = 0.65f;
    private bool _disposed;

    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<SpeechSpeed>? SpeedChanged;

    public SpeechSpeed Speed => _currentSpeed;

    public VoiceListenerService(IReadOnlyList<ICommandHandler> handlers)
    {
        _handlers = handlers;
        _engine   = new SpeechRecognitionEngine(new CultureInfo("en-US"));
        _engine.SetInputToDefaultAudioDevice();
        _engine.SpeechRecognized += OnSpeechRecognized;

        LoadGrammar();
        ApplySpeed(SpeechSpeed.Normal);
    }

    // -------------------------------------------------------------------------
    // Public control
    // -------------------------------------------------------------------------

    public void Start() => _engine.RecognizeAsync(RecognizeMode.Multiple);
    public void Stop()  => _engine.RecognizeAsyncStop();

    /// <summary>Force a specific speed preset (disables auto-detection until next utterance).</summary>
    public void SetSpeed(SpeechSpeed speed)
    {
        _wordRateSamples.Clear();
        ApplySpeed(speed);
    }

    // -------------------------------------------------------------------------
    // Speed presets
    // -------------------------------------------------------------------------

    private void ApplySpeed(SpeechSpeed speed)
    {
        if (speed == _currentSpeed && _wordRateSamples.Count > 1)
            return;

        _currentSpeed = speed;

        var (endSilence, endSilenceAmbiguous, babble, confidence) = speed switch
        {
            SpeechSpeed.Slow   => (1.5, 2.5, 6.0, 0.50f),
            SpeechSpeed.Normal => (0.5, 1.0, 4.0, 0.65f),
            SpeechSpeed.Fast   => (0.2, 0.4, 2.0, 0.70f),
            _ => throw new ArgumentOutOfRangeException(nameof(speed)),
        };

        _engine.EndSilenceTimeout          = TimeSpan.FromSeconds(endSilence);
        _engine.EndSilenceTimeoutAmbiguous  = TimeSpan.FromSeconds(endSilenceAmbiguous);
        _engine.BabbleTimeout               = TimeSpan.FromSeconds(babble);
        _minConfidence                      = confidence;

        SpeedChanged?.Invoke(this, speed);
    }

    // -------------------------------------------------------------------------
    // Auto-detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Measures words-per-second from the recognition result's audio duration
    /// and adjusts the speed preset accordingly.
    /// </summary>
    private void AdaptSpeed(RecognitionResult result)
    {
        var duration = result.Audio?.Duration;
        if (duration is null || duration.Value.TotalSeconds < 0.3)
            return;

        int wordCount = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        double wordsPerSecond = wordCount / duration.Value.TotalSeconds;

        _wordRateSamples.Enqueue(wordsPerSecond);
        while (_wordRateSamples.Count > SampleWindow)
            _wordRateSamples.Dequeue();

        if (_wordRateSamples.Count < 2)
            return;

        double avgRate = _wordRateSamples.Average();

        // Thresholds: < 1.5 wps = slow, 1.5–3.0 wps = normal, > 3.0 wps = fast
        var detected = avgRate switch
        {
            < 1.5 => SpeechSpeed.Slow,
            > 3.0 => SpeechSpeed.Fast,
            _     => SpeechSpeed.Normal,
        };

        if (detected != _currentSpeed)
            ApplySpeed(detected);
    }

    // -------------------------------------------------------------------------
    // Grammar
    // -------------------------------------------------------------------------

    private void LoadGrammar()
    {
        var commandChoices = new Choices();

        foreach (var handler in _handlers)
            commandChoices.Add(handler.BuildGrammar());

        var root = new GrammarBuilder(WakePhrase);
        root.Append(commandChoices);

        _engine.LoadGrammar(new Grammar(root) { Name = "WakeAndCommand" });
    }

    // -------------------------------------------------------------------------
    // Recognition callback
    // -------------------------------------------------------------------------

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        AdaptSpeed(e.Result);

        if (e.Result.Confidence < _minConfidence)
            return;

        foreach (var handler in _handlers)
        {
            var result = handler.TryHandle(e.Result);
            if (result is null) continue;

            CommandExecuted?.Invoke(this, new CommandEventArgs(
                HandlerName:    handler.Name,
                RecognizedText: e.Result.Text,
                Confidence:     e.Result.Confidence,
                Result:         result));
            return;
        }
    }

    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _engine.Dispose();
        _disposed = true;
    }
}

/// <summary>Event data raised after a command is dispatched.</summary>
public sealed record CommandEventArgs(
    string        HandlerName,
    string        RecognizedText,
    float         Confidence,
    CommandResult Result);
