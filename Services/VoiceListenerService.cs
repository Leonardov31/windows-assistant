using System.Globalization;
using System.Text.Json;
using Vosk;
using WindowsAssistant.Commands;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase followed by a registered command,
/// using offline Vosk speech recognition.
///
/// One <see cref="VoskRecognizer"/> is created per detected culture whose model
/// is present on disk (see <see cref="VoskModelSetupService"/>). Microphone
/// audio is captured once (<see cref="AudioCaptureService"/>) and fanned out
/// to every recognizer in parallel — whichever transcribes with the highest
/// confidence wins.
///
/// Automatically adapts the minimum confidence threshold based on observed
/// speaking pace.
/// </summary>
public sealed class VoiceListenerService : IDisposable
{
    private const int SampleWindow = 8;

    private static readonly Dictionary<string, string[]> WakePhrases = new()
    {
        ["en-US"] = ["hey windows", "hey computer"],
        // "windows" is missing from vosk-model-small-pt-0.3's pronunciation
        // dictionary (foreign word), which caused the decoder to either emit
        // [unk] or split the utterance at that token. Using Portuguese words
        // that are guaranteed to be in the lexicon gives reliable wake-phrase
        // recognition on small pt-BR models.
        ["pt-BR"] = ["ei computador", "oi computador", "olá computador", "ola computador"],
    };

    // Cultures that should actually be loaded at runtime.
    private static readonly HashSet<string> EnabledCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "pt-BR",
        "en-US",
    };

    private sealed class RecognizerEntry : IDisposable
    {
        public required CultureInfo Culture { get; init; }
        public required Model Model { get; init; }
        public required VoskRecognizer Recognizer { get; init; }

        public void Dispose()
        {
            Recognizer.Dispose();
            Model.Dispose();
        }
    }

    private readonly List<RecognizerEntry> _engines = new();
    private readonly IReadOnlyList<ICommandHandler> _handlers;
    private readonly AudioCaptureService _audio = new();
    private readonly Queue<double> _wordRateSamples = new();
    private readonly Lock _recognizersLock = new();
    private SpeechSpeed _currentSpeed = SpeechSpeed.Normal;
    private float _minConfidence = 0.65f;
    private bool _disposed;
    private bool _running;

    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<SpeechSpeed>? SpeedChanged;
    public event EventHandler<string>? EngineStatus;

    public SpeechSpeed Speed => _currentSpeed;
    public IReadOnlyList<string> ActiveCultures => _engines.Select(e => e.Culture.Name).ToList();

    static VoiceListenerService()
    {
        // Silence Vosk's very chatty C logger
        Vosk.Vosk.SetLogLevel(-1);
    }

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
                "No Vosk models could be loaded. Make sure the models finished " +
                "downloading under %LOCALAPPDATA%\\WindowsAssistant\\Models\\.");

        ApplySpeed(SpeechSpeed.Normal);

        _audio.DataAvailable += OnAudioAvailable;
    }

    // -------------------------------------------------------------------------
    // Public control
    // -------------------------------------------------------------------------

    public void Start()
    {
        if (_running) return;

        _audio.Start();
        _running = true;

        var devices = AudioCaptureService.EnumerateDevices();
        Log($"[{DateTime.Now:HH:mm:ss}] Cultures loaded: {string.Join(", ", ActiveCultures)}");
        Log($"[{DateTime.Now:HH:mm:ss}] Input devices ({devices.Count}): {string.Join(" | ", devices)}");
        Log($"[{DateTime.Now:HH:mm:ss}] Using device: {_audio.DeviceName}");
        Log($"[{DateTime.Now:HH:mm:ss}] Min confidence: {_minConfidence:P0}");
    }

    public void Stop()
    {
        if (!_running) return;
        _audio.Stop();
        _running = false;
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
        if (!EnabledCultures.Contains(culture.Name))
        {
            EngineStatus?.Invoke(this, $"Skipped: {culture.Name} (disabled)");
            return;
        }

        var modelPath = VoskModelSetupService.GetModelPath(culture.Name);
        if (modelPath is null)
        {
            EngineStatus?.Invoke(this, $"Skipped: {culture.Name} (model not installed)");
            return;
        }

        try
        {
            var model = new Model(modelPath);
            var grammar = BuildGrammarJson(culture);
            var recognizer = new VoskRecognizer(model, AudioCaptureService.SampleRate, grammar);
            recognizer.SetWords(true); // enable per-word confidence in results

            _engines.Add(new RecognizerEntry
            {
                Culture    = culture,
                Model      = model,
                Recognizer = recognizer,
            });

            EngineStatus?.Invoke(this, $"Loaded: {culture.Name}");
        }
        catch (Exception ex)
        {
            EngineStatus?.Invoke(this, $"Failed to load {culture.Name}: {ex.Message}");
        }
    }

    private string BuildGrammarJson(CultureInfo culture)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Wake phrase tokens from EVERY culture go into EVERY recognizer's grammar.
        // Users often mix "hey windows" with a pt-BR command (or vice-versa); if
        // only culture-specific tokens are allowed the recognizer can't transcribe
        // the wake phrase and the whole utterance is lost.
        foreach (var phrase in WakePhrases.Values.SelectMany(v => v))
            foreach (var token in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                words.Add(token);

        foreach (var handler in _handlers)
        {
            if (!handler.SupportedCultures.Any(c => c.Name == culture.Name)) continue;
            foreach (var w in handler.BuildVocabulary(culture))
                words.Add(w);
        }

        // [unk] lets Vosk emit an unknown token for OOV audio instead of misfiring
        var tokens = words.Select(w => w.ToLowerInvariant()).ToList();
        tokens.Add("[unk]");

        return JsonSerializer.Serialize(tokens);
    }

    // -------------------------------------------------------------------------
    // Speed presets (only confidence matters with Vosk; VAD is internal)
    // -------------------------------------------------------------------------

    private void ApplySpeed(SpeechSpeed speed)
    {
        if (speed == _currentSpeed && _wordRateSamples.Count > 1)
            return;

        _currentSpeed = speed;
        _minConfidence = speed switch
        {
            SpeechSpeed.Slow   => 0.50f,
            SpeechSpeed.Normal => 0.65f,
            SpeechSpeed.Fast   => 0.70f,
            _ => throw new ArgumentOutOfRangeException(nameof(speed)),
        };

        SpeedChanged?.Invoke(this, speed);
    }

    // -------------------------------------------------------------------------
    // Speed auto-detection from word rate
    // -------------------------------------------------------------------------

    private void AdaptSpeed(double durationSeconds, int wordCount)
    {
        if (durationSeconds < 0.3 || wordCount == 0) return;

        double wordsPerSecond = wordCount / durationSeconds;
        _wordRateSamples.Enqueue(wordsPerSecond);
        while (_wordRateSamples.Count > SampleWindow)
            _wordRateSamples.Dequeue();

        if (_wordRateSamples.Count < 2) return;

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
    // Audio → recognizers
    // -------------------------------------------------------------------------

    private void OnAudioAvailable(object? sender, byte[] pcm)
    {
        lock (_recognizersLock)
        {
            foreach (var entry in _engines)
            {
                if (!entry.Recognizer.AcceptWaveform(pcm, pcm.Length))
                    continue;

                var json = entry.Recognizer.Result();
                HandleFinalResult(entry.Culture, json);
            }
        }
    }

    private void HandleFinalResult(CultureInfo culture, string json)
    {
        if (!TryParseResult(json, out var rawText, out var confidence, out var durationSeconds))
            return;

        if (string.IsNullOrWhiteSpace(rawText))
            return;

        int wordCount = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        AdaptSpeed(durationSeconds, wordCount);

        // Vosk emits number words (cinco, five, cinquenta, ...) — the regex
        // parsers in the handlers expect digits. Normalize before matching.
        var text = CommandVocabulary.NormalizeNumbers(rawText, culture);

        // Diagnostic logging: every final recognition is logged, with the reason
        // it was kept or dropped. Essential for tuning vocabulary, wake phrases,
        // and confidence thresholds when users report "I spoke but nothing happened".
        string prefix = text == rawText
            ? $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" ({confidence:P0})"
            : $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" (raw: \"{rawText}\") ({confidence:P0})";

        if (confidence < _minConfidence)
        {
            Log($"{prefix} DROPPED: below threshold ({_minConfidence:P0})");
            return;
        }

        if (!StartsWithAnyWakePhrase(text))
        {
            Log($"{prefix} DROPPED: no wake phrase");
            return;
        }

        var output = new RecognitionOutput(text, confidence);

        foreach (var handler in _handlers)
        {
            var result = handler.TryHandle(output);
            if (result is null) continue;

            var command = StripWakePhrase(output.Text);
            var outcome = result.Success ? result.Message : $"FAILED: {result.Message}";
            Log($"{prefix} → [{handler.Name}] {outcome} (command: \"{command}\")");

            CommandExecuted?.Invoke(this, new CommandEventArgs(
                HandlerName:    handler.Name,
                RecognizedText: output.Text,
                Confidence:     output.Confidence,
                Result:         result));
            return;
        }

        // Wake phrase ok, confidence ok, but no handler matched the text pattern
        Log($"{prefix} DROPPED: no handler matched (command: \"{StripWakePhrase(text)}\")");
    }

    /// <summary>
    /// Parses a Vosk final-result JSON payload:
    /// {"text":"...", "result":[{"conf":0.99,"start":0.1,"end":0.5,"word":"..."}, ...]}
    /// Confidence = mean of per-word confidences. Duration = end of last word.
    /// </summary>
    private static bool TryParseResult(string json, out string text, out float confidence, out double durationSeconds)
    {
        text = string.Empty;
        confidence = 0f;
        durationSeconds = 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("text", out var textEl))
                return false;

            text = textEl.GetString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!doc.RootElement.TryGetProperty("result", out var resultEl) ||
                resultEl.ValueKind != JsonValueKind.Array)
            {
                // No per-word data — assume neutral confidence
                confidence = 0.5f;
                return true;
            }

            double confSum = 0;
            int confCount = 0;
            double lastEnd = 0;
            double firstStart = double.MaxValue;

            foreach (var word in resultEl.EnumerateArray())
            {
                if (word.TryGetProperty("conf", out var c))
                {
                    confSum += c.GetDouble();
                    confCount++;
                }
                if (word.TryGetProperty("start", out var s)) firstStart = Math.Min(firstStart, s.GetDouble());
                if (word.TryGetProperty("end",   out var e)) lastEnd    = Math.Max(lastEnd,    e.GetDouble());
            }

            confidence = confCount > 0 ? (float)(confSum / confCount) : 0.5f;
            durationSeconds = firstStart < double.MaxValue ? Math.Max(0, lastEnd - firstStart) : 0;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool StartsWithAnyWakePhrase(string text)
    {
        foreach (var wake in WakePhrases.Values.SelectMany(v => v))
        {
            if (text.StartsWith(wake, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string StripWakePhrase(string text)
    {
        foreach (var wake in WakePhrases.Values.SelectMany(v => v))
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
        Stop();
        _audio.DataAvailable -= OnAudioAvailable;
        _audio.Dispose();
        foreach (var entry in _engines)
            entry.Dispose();
        _disposed = true;
    }
}

/// <summary>Event data raised after a command is dispatched.</summary>
public sealed record CommandEventArgs(
    string        HandlerName,
    string        RecognizedText,
    float         Confidence,
    CommandResult Result);
