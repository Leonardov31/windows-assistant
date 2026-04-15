using System.Diagnostics;
using System.Globalization;
using Windows.Media.SpeechRecognition;
using WindowsAssistant.Commands;
using WindowsAssistant.Infrastructure;

namespace WindowsAssistant.Services;

/// <summary>
/// Continuously listens for the wake phrase followed by a registered command,
/// using Windows' built-in speech recognizer (<see cref="SpeechRecognizer"/>
/// with a <see cref="SpeechRecognitionScenario.Dictation"/> topic constraint).
///
/// Two-phase flow:
/// <list type="number">
///   <item><c>AwaitingWake</c> — the service scans every utterance for the
///     wake phrase. If absent, the utterance is dropped silently (no logs
///     anywhere). If present, <see cref="ChimeService.PlayWakeChime"/> plays,
///     any text before the wake phrase is discarded, and any text after it
///     (≤ 6 words) is tried immediately as a command. Otherwise the service
///     transitions to <c>AwaitingCommand</c>.</item>
///   <item><c>AwaitingCommand</c> — the next full utterance is treated as a
///     candidate command. If it matches a handler (within the 6-word cap)
///     it executes; either way the service returns to <c>AwaitingWake</c>.
///     A 5-second timeout also reverts to <c>AwaitingWake</c>.</item>
/// </list>
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

    /// <summary>
    /// Maximum number of words in the command portion of an utterance
    /// (after the wake phrase). Longer candidates are rejected with a
    /// logged note and the service returns to <see cref="Phase.AwaitingWake"/>.
    /// </summary>
    internal const int MaxCommandWords = 6;

    /// <summary>
    /// How long <see cref="Phase.AwaitingCommand"/> waits for a command
    /// after the wake phrase was heard before giving up and going back
    /// to <see cref="Phase.AwaitingWake"/>.
    /// </summary>
    private static readonly TimeSpan CommandPhaseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Cultures known to the app, in preferred load order.</summary>
    public static readonly IReadOnlyList<string> KnownCultures = new[] { "pt-BR", "en-US" };

    private enum Phase { AwaitingWake, AwaitingCommand }

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
    private readonly Lock _phaseLock = new();
    private RecognizerEntry? _engine;
    private Phase _phase = Phase.AwaitingWake;
    private CancellationTokenSource? _commandTimeoutCts;
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
        LogAlways($"[{DateTime.Now:HH:mm:ss}] Command timeout: {CommandPhaseTimeout.TotalSeconds:0}s, max words: {MaxCommandWords}");

        _ = LoadActiveEngineAsync();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        CancelCommandTimeout();

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

            // Per-recognizer latch: once we've decided the current utterance
            // can't match the wake phrase and cancelled it, ignore further
            // hypotheses until Completed fires and the outer restart logic
            // builds a fresh recognizer (with its own fresh latch).
            //
            // This only runs in AwaitingWake — in AwaitingCommand we need the
            // full utterance, whatever the words are.
            bool utteranceAborted = false;

            recognizer.HypothesisGenerated += (sender, e) =>
            {
                if (CurrentPhase != Phase.AwaitingWake) return;
                if (utteranceAborted) return;

                var hypothesis = (e.Hypothesis.Text ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(hypothesis)) return;

                if (CanStillMatchWakePhrase(hypothesis, _settings.WakePhrase)) return;

                utteranceAborted = true;
                try { _ = sender.ContinuousRecognitionSession.CancelAsync(); }
                catch { /* session may already be tearing down */ }
            };

            await recognizer.ContinuousRecognitionSession.StartAsync();

            lock (_recognizersLock)
            {
                _engine = new RecognizerEntry { Culture = culture, Recognizer = recognizer };
            }

            // Always start a newly-loaded engine in AwaitingWake — any command
            // phase belongs to the previous recognizer, not this one.
            lock (_phaseLock) _phase = Phase.AwaitingWake;
            CancelCommandTimeout();

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
        // should all resume too.
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
    // Phase state machine
    // -------------------------------------------------------------------------

    private Phase CurrentPhase
    {
        get { lock (_phaseLock) return _phase; }
    }

    private void HandleResult(CultureInfo culture, SpeechRecognitionResult result)
    {
        var rawText = (result.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(rawText)) return;

        float confidence = (float)result.RawConfidence;

        // Below-threshold utterances are dropped silently — no log anywhere.
        // (If the user is debugging and wants to see these, they can lower
        // MinConfidence or look at HypothesisGenerated in a debug build.)
        if (confidence < MinConfidence) return;

        Phase phase;
        lock (_phaseLock) phase = _phase;

        if (phase == Phase.AwaitingWake)
            HandleWakePhase(culture, rawText, confidence);
        else
            HandleCommandPhase(culture, rawText, confidence);
    }

    private void HandleWakePhase(CultureInfo culture, string rawText, float confidence)
    {
        if (!TryExtractCommand(rawText, _settings.WakePhrase, out var commandText))
        {
            // No wake phrase in this utterance — stay silent on every sink.
            return;
        }

        // Wake phrase heard. Play the chime, discard anything said before it,
        // and log the detection to both sinks.
        ChimeService.PlayWakeChime();

        string prefix = $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] wake \"{_settings.WakePhrase}\" ({confidence:P0})";

        // Single-utterance wake+command ("computador brilho cinco") — try to
        // execute immediately.
        if (!string.IsNullOrWhiteSpace(commandText))
        {
            LogWakeMatch($"{prefix} + command \"{commandText}\"");
            TryExecuteCommand(culture, commandText, confidence);
            // Either succeeded or failed — we stay in AwaitingWake (any
            // EnterCommandPhase transition from here would be stale).
            return;
        }

        // Wake phrase only — transition to command phase and arm the timeout.
        LogWakeMatch($"{prefix} — listening for command…");
        EnterCommandPhase();
    }

    private void HandleCommandPhase(CultureInfo culture, string rawText, float confidence)
    {
        CancelCommandTimeout();

        string prefix = $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] command \"{rawText}\" ({confidence:P0})";

        TryExecuteCommand(culture, rawText, confidence, prefix);
        EnterWakePhase();
    }

    /// <summary>
    /// Normalizes numbers, enforces the word cap, and runs the utterance
    /// through the registered handlers. Logs outcome (success, FAILED, or
    /// "no handler matched") to both sinks.
    /// </summary>
    private void TryExecuteCommand(CultureInfo culture, string commandText, float confidence, string? prefixOverride = null)
    {
        string prefix = prefixOverride
            ?? $"[{DateTime.Now:HH:mm:ss}] [{culture.Name}] command \"{commandText}\" ({confidence:P0})";

        if (!IsWithinWordLimit(commandText))
        {
            LogWakeMatch($"{prefix} DROPPED: too many words (limit {MaxCommandWords})");
            return;
        }

        // Dictation sometimes emits number words (cinco, five, fifty) — the
        // regex parsers expect digits.
        var normalized = CommandVocabulary.NormalizeNumbers(commandText, culture);
        var output = new RecognitionOutput(normalized, confidence);

        foreach (var handler in _handlers)
        {
            var cmdResult = handler.TryHandle(output);
            if (cmdResult is null) continue;

            var outcome = cmdResult.Success ? cmdResult.Message : $"FAILED: {cmdResult.Message}";
            LogWakeMatch($"{prefix} → [{handler.Name}] {outcome}");

            CommandExecuted?.Invoke(this, new CommandEventArgs(
                HandlerName:    handler.Name,
                RecognizedText: output.Text,
                Confidence:     output.Confidence,
                Result:         cmdResult));
            return;
        }

        LogWakeMatch($"{prefix} DROPPED: no handler matched");
    }

    private void EnterCommandPhase()
    {
        lock (_phaseLock) _phase = Phase.AwaitingCommand;

        var cts = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_phaseLock)
        {
            previous = _commandTimeoutCts;
            _commandTimeoutCts = cts;
        }
        previous?.Cancel();
        previous?.Dispose();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(CommandPhaseTimeout, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // normal path when a command arrived in time
            }

            // Timeout elapsed with no command. Fall back to wake-listening.
            lock (_phaseLock)
            {
                if (_phase != Phase.AwaitingCommand || _commandTimeoutCts != cts) return;
                _phase = Phase.AwaitingWake;
                _commandTimeoutCts = null;
            }
            LogWakeMatch($"[{DateTime.Now:HH:mm:ss}] command timeout — back to awaiting wake phrase");
        });
    }

    private void EnterWakePhase()
    {
        lock (_phaseLock) _phase = Phase.AwaitingWake;
        CancelCommandTimeout();
    }

    private void CancelCommandTimeout()
    {
        CancellationTokenSource? cts;
        lock (_phaseLock)
        {
            cts = _commandTimeoutCts;
            _commandTimeoutCts = null;
        }
        try { cts?.Cancel(); cts?.Dispose(); } catch { }
    }

    // -------------------------------------------------------------------------
    // Pure helpers (unit-tested)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if <paramref name="hypothesis"/> is still plausibly
    /// building towards the wake phrase (or has already passed it). Used
    /// by the HypothesisGenerated early-abort latch. Both inputs must
    /// already be lowercased/trimmed.
    ///
    /// Early dictation hypotheses get revised heavily ("como" → "computa" →
    /// "computador"), so any prefix check on a too-short hypothesis produces
    /// false aborts. We keep listening until the hypothesis has at least
    /// as many characters as the wake phrase. Once it does, it must start
    /// with the wake phrase.
    /// </summary>
    internal static bool CanStillMatchWakePhrase(string hypothesis, string wakePhrase)
    {
        if (string.IsNullOrWhiteSpace(wakePhrase)) return true;
        if (hypothesis.Length < wakePhrase.Length) return true;

        return hypothesis.StartsWith(wakePhrase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scans <paramref name="text"/> for <paramref name="wakePhrase"/> anywhere
    /// in the string (not just at the start) and returns everything after it
    /// as <paramref name="commandTail"/>. Anything before the wake phrase is
    /// treated as ignored-prefix noise ("uh, computador brilho cinco" → command
    /// tail is "brilho cinco").
    ///
    /// Returns <c>false</c> if the wake phrase is not present.
    /// </summary>
    internal static bool TryExtractCommand(string text, string wakePhrase, out string commandTail)
    {
        commandTail = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(wakePhrase))
            return false;

        int idx = text.IndexOf(wakePhrase, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var tail = text[(idx + wakePhrase.Length)..];
        // Trim leading punctuation the engine sometimes inserts ("computador, ...").
        commandTail = tail.TrimStart(' ', ',', '.', ';', ':', '!', '?').Trim();
        return true;
    }

    /// <summary>
    /// True if the command portion has <see cref="MaxCommandWords"/> or fewer
    /// space-separated tokens. Empty string counts as 0 words → true.
    /// </summary>
    internal static bool IsWithinWordLimit(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return true;
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= MaxCommandWords;
    }

    // -------------------------------------------------------------------------
    // Logging
    //
    // - LogAlways: terminal only (Console + Trace). Used ONLY for engine
    //   lifecycle — startup config lines, "Loaded: culture", session restart
    //   notes, configuration changes. Never for speech content.
    // - LogWakeMatch: terminal AND voice.log. Used for every line that
    //   exists because the wake phrase was detected — successful commands,
    //   handler failures, unmatched commands, word-limit drops, command
    //   timeouts. Non-wake speech never reaches either sink.
    // -------------------------------------------------------------------------

    private static void LogAlways(string message)
    {
        try { Console.WriteLine(message); } catch { }
        try { Trace.WriteLine(message); } catch { }
    }

    private static void LogWakeMatch(string message)
    {
        try { Console.WriteLine(message); } catch { }
        try { Trace.WriteLine(message); } catch { }

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
