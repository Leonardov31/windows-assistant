using System.Globalization;
using System.Speech.Recognition;
using WindowsAssistant.Commands;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase followed by a registered command.
/// Creates one <see cref="SpeechRecognitionEngine"/> per detected culture so
/// English and Portuguese commands work simultaneously.
///
/// Automatically adapts recognition timeouts based on observed speaking pace.
/// </summary>
public sealed class VoiceListenerService : IDisposable
{
    private const int SampleWindow = 8;

    private static readonly Dictionary<string, string> WakePhrases = new()
    {
        ["en-US"] = "hey windows",
        ["pt-BR"] = "ei windows",
    };

    private readonly List<SpeechRecognitionEngine> _engines = new();
    private readonly IReadOnlyList<ICommandHandler> _handlers;
    private readonly Queue<double> _wordRateSamples = new();
    private SpeechSpeed _currentSpeed = SpeechSpeed.Normal;
    private float _minConfidence = 0.65f;
    private bool _disposed;

    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<SpeechSpeed>? SpeedChanged;
    public event EventHandler<string>? EngineStatus;

    public SpeechSpeed Speed => _currentSpeed;
    public IReadOnlyList<string> ActiveCultures => _engines.Select(e => e.RecognizerInfo.Culture.Name).ToList();

    public VoiceListenerService(IReadOnlyList<ICommandHandler> handlers)
    {
        _handlers = handlers;

        var cultures = handlers
            .SelectMany(h => h.SupportedCultures)
            .Distinct()
            .ToList();

        foreach (var culture in cultures)
            TryCreateEngine(culture);

        if (_engines.Count == 0)
            throw new InvalidOperationException(
                "No speech recognition engines could be created. " +
                "Install a Windows language pack with speech recognition support.");

        ApplySpeed(SpeechSpeed.Normal);
    }

    // -------------------------------------------------------------------------
    // Public control
    // -------------------------------------------------------------------------

    public void Start()
    {
        foreach (var engine in _engines)
            engine.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void Stop()
    {
        foreach (var engine in _engines)
            engine.RecognizeAsyncStop();
    }

    public void SetSpeed(SpeechSpeed speed)
    {
        _wordRateSamples.Clear();
        ApplySpeed(speed);
    }

    // -------------------------------------------------------------------------
    // Engine factory
    // -------------------------------------------------------------------------

    private void TryCreateEngine(CultureInfo culture)
    {
        try
        {
            var engine = new SpeechRecognitionEngine(culture);
            engine.SetInputToDefaultAudioDevice();
            engine.SpeechRecognized += OnSpeechRecognized;

            LoadGrammar(engine, culture);
            _engines.Add(engine);

            EngineStatus?.Invoke(this, $"Loaded: {culture.Name}");
        }
        catch (Exception)
        {
            // Culture not available — language pack not installed, skip silently
            EngineStatus?.Invoke(this, $"Skipped: {culture.Name} (not installed)");
        }
    }

    private void LoadGrammar(SpeechRecognitionEngine engine, CultureInfo culture)
    {
        string wake = WakePhrases.GetValueOrDefault(culture.Name, "hey windows");

        var commandChoices = new Choices();
        foreach (var handler in _handlers)
        {
            if (handler.SupportedCultures.Any(c => c.Name == culture.Name))
                commandChoices.Add(handler.BuildGrammar(culture));
        }

        var root = new GrammarBuilder(wake);
        root.Append(commandChoices);

        engine.LoadGrammar(new Grammar(root) { Name = $"WakeAndCommand_{culture.Name}" });
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

        foreach (var engine in _engines)
        {
            engine.EndSilenceTimeout         = TimeSpan.FromSeconds(endSilence);
            engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(endSilenceAmbiguous);
            engine.BabbleTimeout              = TimeSpan.FromSeconds(babble);
        }

        _minConfidence = confidence;
        SpeedChanged?.Invoke(this, speed);
    }

    // -------------------------------------------------------------------------
    // Auto-detection
    // -------------------------------------------------------------------------

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

            var command = StripWakePhrase(e.Result.Text);
            var outcome = result.Success ? result.Message : $"FAILED: {result.Message}";
            Log($"[{DateTime.Now:HH:mm:ss}] \"{command}\" ({e.Result.Confidence:P0}) → [{handler.Name}] {outcome}");

            CommandExecuted?.Invoke(this, new CommandEventArgs(
                HandlerName:    handler.Name,
                RecognizedText: e.Result.Text,
                Confidence:     e.Result.Confidence,
                Result:         result));
            return;
        }
    }

    private static string StripWakePhrase(string text)
    {
        foreach (var wake in WakePhrases.Values)
        {
            if (text.StartsWith(wake, StringComparison.OrdinalIgnoreCase))
                return text[wake.Length..].TrimStart();
        }
        return text;
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);

        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsAssistant");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "voice.log"), message + Environment.NewLine);
        }
        catch
        {
            // Don't let logging failures break recognition
        }
    }

    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var engine in _engines)
            engine.Dispose();
        _disposed = true;
    }
}

/// <summary>Event data raised after a command is dispatched.</summary>
public sealed record CommandEventArgs(
    string        HandlerName,
    string        RecognizedText,
    float         Confidence,
    CommandResult Result);
