# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build WindowsAssistant.csproj            # debug build
dotnet build WindowsAssistant.csproj -c Release  # release build
```

### Publish (self-contained single-file to ./dist)

```bash
taskkill //F //IM WindowsAssistant.exe           # stop running instance first
dotnet publish WindowsAssistant.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./dist
```

Note: trimming (`PublishTrimmed`) is incompatible with Windows Forms. Always publish to `./dist`.

## Architecture

Windows-only system tray app (.NET 10 / WinForms) that listens for voice commands and controls monitors via DDC/CI.

### Voice recognition

`VoiceListenerService` uses the built-in `Windows.Media.SpeechRecognition` API (WinRT / OneCore). One `SpeechRecognizer` is active at a time — the one for `AppSettings.ActiveCulture`. Recognition runs via a `ContinuousRecognitionSession` configured with a `SpeechRecognitionTopicConstraint(Dictation, "command")`, so any utterance gets transcribed; a small phase state machine filters the results.

**Two-phase flow:**

1. `Phase.AwaitingWake` — every utterance is scanned for the wake phrase (anywhere in the text, not just the start). If absent, the utterance is dropped silently and nothing is written to either sink. If present, `ChimeService.PlayWakeChime()` plays the Windows `Asterisk` system sound, anything before the wake phrase is discarded, and everything after is treated as a candidate command. If there's trailing text it's tried immediately (single-breath "computador brilho cinco"); otherwise the service transitions to `AwaitingCommand`.
2. `Phase.AwaitingCommand` — the next utterance is the command candidate. Rejected if longer than `MaxCommandWords` (6). Otherwise `CommandVocabulary.NormalizeNumbers` runs and handlers are tried in order. Either way the service returns to `AwaitingWake`. A 5-second `CancellationTokenSource`-driven timeout also reverts to `AwaitingWake` if no command arrives.

`HypothesisGenerated` has a phase-aware early-abort latch: during `AwaitingWake` it cancels the session as soon as the partial hypothesis is clearly not the wake phrase (via `CanStillMatchWakePhrase`). Disabled during `AwaitingCommand` so the full command utterance can resolve.

The wake phrase is user-defined (`AppSettings.WakePhrase`, default `computador`). Confidence threshold is a fixed `MinConfidence = 0.5f`. Switching languages disposes the current recognizer and rebuilds via `SetActiveCulture(name)`.

If the Windows speech language pack for `ActiveCulture` isn't installed, the engine is skipped with an `EngineStatus` message pointing at Settings → Time & language → Language. Microphone permission denied yields a similar message. Session completions (including the periodic `UserCanceled` that Windows emits on unpackaged dictation) auto-restart by rebuilding the recognizer after 500 ms.

### Audio pipeline

The OS owns audio capture — no NAudio, no manual mic enumeration. `SpeechRecognizer` uses whichever microphone Windows treats as the default input device, respecting the privacy permission for microphone access.

### Service wiring

`TrayApplication` (in `UI/`) owns the lifecycle. Its constructor:

1. Loads `AppSettings` from `%LOCALAPPDATA%/WindowsAssistant/settings.json`.
2. Creates `MonitorControlService` — enumerates physical monitors, exposes DDC/CI brightness get/set via P/Invoke to `dxva2.dll`.
3. Builds a list of `ICommandHandler` implementations (`BrightnessCommandHandler`, `MonitorPowerCommandHandler`) — both use `CommandVocabulary` for shared parsing.
4. Creates `VoiceListenerService(handlers, settings)` and calls `Start()`, which asynchronously loads the `SpeechRecognizer` for `settings.ActiveCulture`.

### Adding a new voice command

1. Create a class implementing `ICommandHandler` in `Commands/`
2. Declare `SupportedCultures` (e.g. `[new("en-US"), new("pt-BR")]`)
3. `BuildVocabulary(CultureInfo culture)` is not consumed by the WinRT listener (dictation is free-form) but the interface still requires it — return `[]` if you have nothing to declare
4. `TryHandle(RecognitionOutput output)` parses `output.Text` with regex and executes the action, returning `CommandResult` or `null`
5. Register it in `TrayApplication.BuildHandlers()`
6. Update the help text in `UI/HelpDialog.cs`
7. Update `README.md` with the new command examples

### Key conventions

- `NativeMethods` (in `Infrastructure/`) centralizes all P/Invoke declarations — add new Win32 interop there. `ConsoleAttach.EnsureAttached()` attaches the WinExe to a parent terminal so diagnostic log lines stream live.
- Wake phrase and active culture are persisted in `AppSettings`; the tray menu (`Language` submenu + `Wake phrase...` dialog) mutates them at runtime.
- Monitor indices are 1-based in voice commands, 0-based internally.
- `CommandVocabulary` (in `Commands/`) centralizes all language words, ordinals, power states, and parsing — edit here to add new words or languages.
- Brightness values 0–10 map to 0%–100% (×10); values 11–100 are direct percentages.
- Monitor power uses VCP code 0xD6 via `SetVCPFeature` (1 = on, 4 = standby).
- Crash logs are written to `%LOCALAPPDATA%/WindowsAssistant/crash.log`.
- Voice logging follows the two-phase rule: **nothing** is logged (terminal or file) for utterances that never had the wake phrase. Once the wake phrase is detected, every subsequent line — the detection itself, command outcomes, handler failures, word-limit drops, command timeouts — is written to both the terminal (Console + Trace) and `%LOCALAPPDATA%/WindowsAssistant/voice.log` via `LogWakeMatch`. `LogAlways` is reserved for engine lifecycle (startup config, `Loaded: culture`, session restarts).
- Minimum confidence threshold is the fixed `VoiceListenerService.MinConfidence = 0.5f` (the `SpeechRecognitionResult.RawConfidence` float).
- Command cap: `VoiceListenerService.MaxCommandWords = 6`. Command timeout: 5 s (`CommandPhaseTimeout`). Both documented inline in `VoiceListenerService`.
- Wake-detect chime: `Infrastructure/ChimeService.PlayWakeChime()` wraps `System.Media.SystemSounds.Asterisk.Play()`. Respects user's Windows sound scheme; no WAV resources to ship.
