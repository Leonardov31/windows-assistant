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

`VoiceListenerService` uses the built-in `Windows.Media.SpeechRecognition` API (WinRT / OneCore). One `SpeechRecognizer` is active at a time — the one for `AppSettings.ActiveCulture`. Recognition runs via a `ContinuousRecognitionSession` configured with a `SpeechRecognitionTopicConstraint(Dictation, "command")`, so any utterance gets transcribed; the service then enforces the wake-phrase prefix + `MinConfidence = 0.65f` threshold in code.

The wake phrase is user-defined (`AppSettings.WakePhrase`, default `computador`) and dictation grammar does not need to be rebuilt when it changes. Switching languages disposes the current recognizer and constructs a new one via `SetActiveCulture(name)`.

If the Windows speech language pack for `ActiveCulture` isn't installed, the engine is skipped with an `EngineStatus` message pointing the user at Settings → Time & language → Language. Microphone permission denied yields a similar message. Recoverable session failures (`TimeoutExceeded`, `AudioQualityFailure`) auto-restart after 500 ms.

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
- Voice logging is split: **every** transcription (including dropped ones) streams to the terminal (Console + Trace); only wake-phrase-matched utterances are appended to `%LOCALAPPDATA%/WindowsAssistant/voice.log`.
- Minimum confidence threshold is the fixed `VoiceListenerService.MinConfidence = 0.65f` (the `SpeechRecognitionResult.RawConfidence` float).
