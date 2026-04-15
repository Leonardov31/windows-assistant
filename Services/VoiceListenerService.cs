using System.Globalization;
using System.Text.Json;
using Vosk;
using WindowsAssistant.Commands;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase followed by a registered command,
/// using offline Vosk speech recognition.
///
/// Only one <see cref="VoskRecognizer"/> is active at a time — the one for
/// <see cref="AppSettings.ActiveCulture"/>. The wake phrase is user-defined
/// (<see cref="AppSettings.WakePhrase"/>); its tokens are injected into the
/// grammar alongside each handler's vocabulary. Switching language or wake
/// phrase disposes the current model and reloads.
/// </summary>
public sealed class VoiceListenerService : IDisposable
{
    /// <summary>
    /// Recognitions with a mean per-word confidence below this threshold are
    /// dropped. 0.65 was chosen empirically from voice.log samples on the
    /// small-pt-0.3 and small-en-us-0.15 models.
    /// </summary>
    private const float MinConfidence = 0.65f;

    /// <summary>Cultures known to the app, in preferred load order.</summary>
    public static readonly IReadOnlyList<string> KnownCultures = new[] { "pt-BR", "en-US" };

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

    private readonly IReadOnlyList<ICommandHandler> _handlers;
    private readonly AppSettings _settings;
    private readonly AudioCaptureService _audio = new();
    private readonly Lock _recognizersLock = new();
    private RecognizerEntry? _engine;
    private bool _disposed;
    private bool _running;

    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<string>? EngineStatus;
    public event EventHandler? ConfigurationChanged;

    public string ActiveCulture => _settings.ActiveCulture;
    public string WakePhrase    => _settings.WakePhrase;
    public bool IsRunning       => _running;

    static VoiceListenerService()
    {
        // Silence Vosk's very chatty C logger
        Vosk.Vosk.SetLogLevel(-1);
    }

    public VoiceListenerService(IReadOnlyList<ICommandHandler> handlers, AppSettings settings)
    {
        _handlers = handlers;
        _settings = settings;

        LoadActiveEngine();

        if (_engine is null)
            throw new InvalidOperationException(
                "No Vosk model could be loaded. Make sure the models finished " +
                "downloading under %LOCALAPPDATA%\\WindowsAssistant\\Models\\.");

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
        Log($"[{DateTime.Now:HH:mm:ss}] Active culture: {_settings.ActiveCulture}");
        Log($"[{DateTime.Now:HH:mm:ss}] Wake phrase: \"{_settings.WakePhrase}\"");
        Log($"[{DateTime.Now:HH:mm:ss}] Input devices ({devices.Count}): {string.Join(" | ", devices)}");
        Log($"[{DateTime.Now:HH:mm:ss}] Using device: {_audio.DeviceName}");
        Log($"[{DateTime.Now:HH:mm:ss}] Min confidence: {MinConfidence:P0}");
    }

    public void Stop()
    {
        if (!_running) return;
        _audio.Stop();
        _running = false;
    }

    /// <summary>Switches the active culture, reloading the Vosk model.</summary>
    public void SetActiveCulture(string cultureName)
    {
        if (string.Equals(_settings.ActiveCulture, cultureName, StringComparison.OrdinalIgnoreCase))
            return;

        _settings.ActiveCulture = cultureName;
        _settings.Save();
        ReloadEngine();

        Log($"[{DateTime.Now:HH:mm:ss}] Active culture changed: {cultureName}");
    }

    /// <summary>Updates the wake phrase and rebuilds the current recognizer.</summary>
    public void SetWakePhrase(string phrase)
    {
        var normalized = (phrase ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return;
        if (normalized == _settings.WakePhrase) return;

        _settings.WakePhrase = normalized;
        _settings.Save();
        ReloadEngine();

        Log($"[{DateTime.Now:HH:mm:ss}] Wake phrase changed: \"{normalized}\"");
    }

    // -------------------------------------------------------------------------
    // Engine lifecycle
    // -------------------------------------------------------------------------

    private void LoadActiveEngine()
    {
        var culture  = new CultureInfo(_settings.ActiveCulture);
        var modelPath = VoskModelSetupService.GetModelPath(culture.Name);
        if (modelPath is null)
        {
            EngineStatus?.Invoke(this, $"Skipped: {culture.Name} (model not installed)");
            return;
        }

        try
        {
            var model      = new Model(modelPath);
            var grammar    = BuildGrammarJson(culture);
            var recognizer = new VoskRecognizer(model, AudioCaptureService.SampleRate, grammar);
            recognizer.SetWords(true);

            lock (_recognizersLock)
            {
                _engine = new RecognizerEntry
                {
                    Culture    = culture,
                    Model      = model,
                    Recognizer = recognizer,
                };
            }

            EngineStatus?.Invoke(this, $"Loaded: {culture.Name}");
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            EngineStatus?.Invoke(this, $"Failed to load {culture.Name}: {ex.Message}");
        }
    }

    private void ReloadEngine()
    {
        RecognizerEntry? old;
        lock (_recognizersLock)
        {
            old = _engine;
            _engine = null;
        }
        old?.Dispose();
        LoadActiveEngine();
    }

    private string BuildGrammarJson(CultureInfo culture)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Wake phrase tokens — user-configurable, may contain multiple words
        foreach (var token in _settings.WakePhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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
    // Audio → recognizer
    // -------------------------------------------------------------------------

    private void OnAudioAvailable(object? sender, byte[] pcm)
    {
        RecognizerEntry? entry;
        lock (_recognizersLock)
            entry = _engine;

        if (entry is null) return;

        if (!entry.Recognizer.AcceptWaveform(pcm, pcm.Length))
            return;

        var json = entry.Recognizer.Result();
        HandleFinalResult(entry.Culture, json);
    }

    private void HandleFinalResult(CultureInfo culture, string json)
    {
        if (!TryParseResult(json, out var rawText, out var confidence))
            return;

        if (string.IsNullOrWhiteSpace(rawText))
            return;

        // Vosk emits number words (cinco, five, cinquenta, ...) — the regex
        // parsers in the handlers expect digits. Normalize before matching.
        var text = CommandVocabulary.NormalizeNumbers(rawText, culture);

        // Diagnostic logging: every final recognition is logged, with the reason
        // it was kept or dropped. Essential for tuning vocabulary and wake phrases.
        string prefix = text == rawText
            ? $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" ({confidence:P0})"
            : $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" (raw: \"{rawText}\") ({confidence:P0})";

        if (confidence < MinConfidence)
        {
            Log($"{prefix} DROPPED: below threshold ({MinConfidence:P0})");
            return;
        }

        if (!text.StartsWith(_settings.WakePhrase, StringComparison.OrdinalIgnoreCase))
        {
            Log($"{prefix} DROPPED: no wake phrase");
            return;
        }

        var output = new RecognitionOutput(text, confidence);

        foreach (var handler in _handlers)
        {
            var result = handler.TryHandle(output);
            if (result is null) continue;

            var command = text[_settings.WakePhrase.Length..].TrimStart();
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
        var stripped = text[_settings.WakePhrase.Length..].TrimStart();
        Log($"{prefix} DROPPED: no handler matched (command: \"{stripped}\")");
    }

    /// <summary>
    /// Parses a Vosk final-result JSON payload:
    /// {"text":"...", "result":[{"conf":0.99,"word":"..."}, ...]}
    /// Confidence is the mean of per-word confidences.
    /// </summary>
    private static bool TryParseResult(string json, out string text, out float confidence)
    {
        text = string.Empty;
        confidence = 0f;

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
            foreach (var word in resultEl.EnumerateArray())
            {
                if (word.TryGetProperty("conf", out var c))
                {
                    confSum += c.GetDouble();
                    confCount++;
                }
            }

            confidence = confCount > 0 ? (float)(confSum / confCount) : 0.5f;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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
        lock (_recognizersLock)
        {
            _engine?.Dispose();
            _engine = null;
        }
        _disposed = true;
    }
}

/// <summary>Event data raised after a command is dispatched.</summary>
public sealed record CommandEventArgs(
    string        HandlerName,
    string        RecognizedText,
    float         Confidence,
    CommandResult Result);
