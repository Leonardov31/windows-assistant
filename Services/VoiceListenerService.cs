using System.Diagnostics;
using System.Globalization;
using Windows.Media.SpeechRecognition;
using WindowsAssistant.Commands;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase followed by a registered command,
/// using Windows' built-in speech recognizer (<see cref="SpeechRecognizer"/>
/// with a <see cref="SpeechRecognitionScenario.Dictation"/> topic constraint).
///
/// Only one <see cref="SpeechRecognizer"/> is active at a time — the one for
/// <see cref="AppSettings.ActiveCulture"/>. The wake phrase is user-defined
/// (<see cref="AppSettings.WakePhrase"/>); dictation is free-form so changing
/// it does not require rebuilding any grammar.
/// </summary>
public sealed class VoiceListenerService : IDisposable
{
    /// <summary>
    /// Recognitions with a raw confidence below this threshold are dropped.
    /// 0.5 matches the value used during the Vosk era; tune in voice.log if
    /// the Windows engine behaves differently for a given language.
    /// </summary>
    private const float MinConfidence = 0.5f;

    /// <summary>Cultures known to the app, in preferred load order.</summary>
    public static readonly IReadOnlyList<string> KnownCultures = new[] { "pt-BR", "en-US" };

    private sealed class RecognizerEntry : IDisposable
    {
        public required CultureInfo Culture { get; init; }
        public required SpeechRecognizer Recognizer { get; init; }

        public void Dispose()
        {
            try { Recognizer.ContinuousRecognitionSession.CancelAsync().AsTask().Wait(500); }
            catch { /* session may already be stopped */ }
            Recognizer.Dispose();
        }
    }

    private readonly IReadOnlyList<ICommandHandler> _handlers;
    private readonly AppSettings _settings;
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

    public VoiceListenerService(IReadOnlyList<ICommandHandler> handlers, AppSettings settings)
    {
        _handlers = handlers;
        _settings = settings;

        // Every engine-lifecycle message also streams to the terminal so the
        // user can see "language pack missing", "mic denied", restart reasons,
        // etc. without having to rig up a UI subscriber.
        EngineStatus += (_, message) =>
            LogAlways($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    // -------------------------------------------------------------------------
    // Public control
    // -------------------------------------------------------------------------

    public void Start()
    {
        if (_running) return;
        _running = true;

        LogAlways($"[{DateTime.Now:HH:mm:ss}] Active culture: {_settings.ActiveCulture}");
        LogAlways($"[{DateTime.Now:HH:mm:ss}] Wake phrase: \"{_settings.WakePhrase}\"");
        LogAlways($"[{DateTime.Now:HH:mm:ss}] Min confidence: {MinConfidence:P0}");

        _ = LoadActiveEngineAsync();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        RecognizerEntry? entry;
        lock (_recognizersLock) entry = _engine;

        if (entry is not null)
        {
            try { _ = entry.Recognizer.ContinuousRecognitionSession.CancelAsync(); }
            catch { /* best effort */ }
        }
    }

    /// <summary>Switches the active culture, reloading the speech recognizer.</summary>
    public void SetActiveCulture(string cultureName)
    {
        if (string.Equals(_settings.ActiveCulture, cultureName, StringComparison.OrdinalIgnoreCase))
            return;

        _settings.ActiveCulture = cultureName;
        _settings.Save();
        _ = ReloadEngineAsync();

        LogAlways($"[{DateTime.Now:HH:mm:ss}] Active culture changed: {cultureName}");
    }

    /// <summary>Updates the wake phrase. Dictation grammar doesn't need rebuilding.</summary>
    public void SetWakePhrase(string phrase)
    {
        var normalized = (phrase ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return;
        if (normalized == _settings.WakePhrase) return;

        _settings.WakePhrase = normalized;
        _settings.Save();

        LogAlways($"[{DateTime.Now:HH:mm:ss}] Wake phrase changed: \"{normalized}\"");
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    // -------------------------------------------------------------------------
    // Engine lifecycle
    // -------------------------------------------------------------------------

    private async Task LoadActiveEngineAsync()
    {
        var culture = new CultureInfo(_settings.ActiveCulture);
        var lang    = new Windows.Globalization.Language(culture.Name);

        if (!SpeechRecognizer.SupportedTopicLanguages.Any(
                l => string.Equals(l.LanguageTag, lang.LanguageTag, StringComparison.OrdinalIgnoreCase)))
        {
            EngineStatus?.Invoke(this,
                $"Skipped: {culture.Name} (Windows speech language pack not installed). " +
                $"Settings → Time & language → Language → install the speech features for {culture.DisplayName}.");
            return;
        }

        SpeechRecognizer? recognizer = null;
        try
        {
            recognizer = new SpeechRecognizer(lang);
            recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "command"));

            // Defaults for continuous dictation are surprisingly aggressive
            // (~5 s initial silence, ~2 s end silence). In a tray app that may
            // sit idle for minutes before the user speaks, the short initial
            // timeout causes the session to Complete with UserCanceled before
            // a single word arrives. Push all three out.
            recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromHours(1);
            recognizer.Timeouts.BabbleTimeout         = TimeSpan.FromHours(1);
            recognizer.Timeouts.EndSilenceTimeout     = TimeSpan.FromSeconds(1.2);

            var compilation = await recognizer.CompileConstraintsAsync();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                EngineStatus?.Invoke(this,
                    $"Failed to compile constraints for {culture.Name}: {compilation.Status}");
                recognizer.Dispose();
                return;
            }

            recognizer.ContinuousRecognitionSession.ResultGenerated +=
                (_, e) => HandleResult(culture, e.Result);
            recognizer.ContinuousRecognitionSession.Completed +=
                (s, e) => OnSessionCompleted(culture, e);

            // Diagnostic: stream partial hypotheses to the terminal so the
            // user can see the recognizer is actually listening. Never
            // written to voice.log — it's high-frequency noise.
            recognizer.HypothesisGenerated += (_, e) =>
                LogAlways($"[{DateTime.Now:HH:mm:ss}] … \"{e.Hypothesis.Text}\"");

            recognizer.StateChanged += (_, e) =>
                LogAlways($"[{DateTime.Now:HH:mm:ss}] State: {e.State}");

            await recognizer.ContinuousRecognitionSession.StartAsync();

            lock (_recognizersLock)
            {
                _engine = new RecognizerEntry { Culture = culture, Recognizer = recognizer };
            }

            EngineStatus?.Invoke(this, $"Loaded: {culture.Name}");
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (UnauthorizedAccessException)
        {
            recognizer?.Dispose();
            EngineStatus?.Invoke(this,
                "Microphone access denied. Allow it under Settings → Privacy & security → Microphone, then restart.");
        }
        catch (Exception ex)
        {
            recognizer?.Dispose();
            EngineStatus?.Invoke(this, $"Failed to load {culture.Name}: {ex.Message}");
        }
    }

    private async Task ReloadEngineAsync()
    {
        RecognizerEntry? old;
        lock (_recognizersLock)
        {
            old = _engine;
            _engine = null;
        }
        old?.Dispose();

        if (_running)
            await LoadActiveEngineAsync();
    }

    private void OnSessionCompleted(
        CultureInfo culture,
        SpeechContinuousRecognitionCompletedEventArgs e)
    {
        if (_disposed || !_running) return;

        // Dictation sessions on unpackaged desktop apps periodically complete
        // on their own — the OS cycles the underlying pipeline. UserCanceled
        // is what Windows reports in that case even though nobody called
        // CancelAsync. Just restart. Success/TimeoutExceeded/AudioQuality
        // should all resume too. The only status we don't auto-restart on is
        // Unknown, where something is fundamentally broken.
        EngineStatus?.Invoke(this, $"Session completed ({e.Status}) — restarting.");
        _ = RestartSessionAfterDelayAsync(culture);
    }

    private async Task RestartSessionAfterDelayAsync(CultureInfo culture)
    {
        await Task.Delay(500);
        if (_disposed || !_running) return;

        // Reusing the existing SpeechRecognizer after Completed is unreliable:
        // StartAsync will often transition the state to Capturing, but the
        // underlying pipeline stays dead and no results ever arrive. Tear the
        // whole recognizer down and build a fresh one instead.
        RecognizerEntry? old;
        lock (_recognizersLock)
        {
            old = _engine;
            if (old is not null && old.Culture.Name != culture.Name)
            {
                // User switched culture since this restart was queued — bail.
                return;
            }
            _engine = null;
        }
        old?.Dispose();

        if (_disposed || !_running) return;
        await LoadActiveEngineAsync();
    }

    // -------------------------------------------------------------------------
    // Recognition dispatch
    // -------------------------------------------------------------------------

    private void HandleResult(CultureInfo culture, SpeechRecognitionResult result)
    {
        var rawText = (result.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(rawText)) return;

        float confidence = (float)result.RawConfidence;

        // Windows' dictation engine sometimes emits number words
        // (cinco, five, fifty) — the regex parsers in the handlers expect
        // digits. Normalize before matching.
        var text = CommandVocabulary.NormalizeNumbers(rawText, culture);

        string prefix = text == rawText
            ? $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" ({confidence:P0})"
            : $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] \"{text}\" (raw: \"{rawText}\") ({confidence:P0})";

        if (confidence < MinConfidence)
        {
            LogAlways($"{prefix} DROPPED: below threshold ({MinConfidence:P0})");
            return;
        }

        if (!text.StartsWith(_settings.WakePhrase, StringComparison.OrdinalIgnoreCase))
        {
            LogAlways($"{prefix} DROPPED: no wake phrase");
            return;
        }

        var output = new RecognitionOutput(text, confidence);

        foreach (var handler in _handlers)
        {
            var cmdResult = handler.TryHandle(output);
            if (cmdResult is null) continue;

            var command = text[_settings.WakePhrase.Length..].TrimStart();
            var outcome = cmdResult.Success ? cmdResult.Message : $"FAILED: {cmdResult.Message}";
            LogWakeMatch($"{prefix} → [{handler.Name}] {outcome} (command: \"{command}\")");

            CommandExecuted?.Invoke(this, new CommandEventArgs(
                HandlerName:    handler.Name,
                RecognizedText: output.Text,
                Confidence:     output.Confidence,
                Result:         cmdResult));
            return;
        }

        // Wake phrase ok, confidence ok, but no handler matched the text pattern
        var stripped = text[_settings.WakePhrase.Length..].TrimStart();
        LogWakeMatch($"{prefix} DROPPED: no handler matched (command: \"{stripped}\")");
    }

    // -------------------------------------------------------------------------
    // Logging
    //
    // - LogAlways: terminal only (Console + Trace). Every transcription and
    //   every lifecycle line goes here so the user can watch recognition live.
    // - LogWakeMatch: terminal AND voice.log. Only fires once the wake phrase
    //   has been confirmed, so the file stays focused on actual user intent
    //   instead of filling up with random background speech.
    // -------------------------------------------------------------------------

    private static void LogAlways(string message)
    {
        try { Console.WriteLine(message); } catch { }
        try { Trace.WriteLine(message); } catch { }
    }

    private static void LogWakeMatch(string message)
    {
        LogAlways(message);

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
        _disposed = true;
        Stop();

        lock (_recognizersLock)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}

/// <summary>Event data raised after a command is dispatched.</summary>
public sealed record CommandEventArgs(
    string        HandlerName,
    string        RecognizedText,
    float         Confidence,
    CommandResult Result);
