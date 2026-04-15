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

### Multi-language voice recognition

`VoiceListenerService` uses offline [Vosk](https://alphacephei.com/vosk/) speech recognition. It creates one `Vosk.Model` + `Vosk.VoskRecognizer` per culture (en-US, pt-BR), each with its own JSON grammar (flat word list including wake-phrase tokens plus each handler's vocabulary).

Wake phrases (can be extended in `VoiceListenerService.WakePhrases`):
- en-US: "hey windows"
- pt-BR: "ei windows" / "oi windows" / "olá windows"

If a Vosk model isn't present on disk, that culture is silently skipped.

### Audio pipeline

`AudioCaptureService` wraps `NAudio.Wave.WaveInEvent` at **16 kHz / 16-bit / mono** — Vosk's required format. A single capture is fanned out to every recognizer; whichever transcribes above the confidence threshold wins. Utterances that don't start with a wake phrase are dropped.

### Service wiring

`TrayApplication` (in `UI/`) owns the lifecycle. Its constructor:
1. Runs `VoskModelSetupService.EnsureModelsAvailable()` — downloads missing Vosk models from alphacephei.com into `%LOCALAPPDATA%/WindowsAssistant/Models/{culture}/`. No admin / UAC elevation needed.
2. Creates `MonitorControlService` — enumerates physical monitors, exposes DDC/CI brightness get/set via P/Invoke to `dxva2.dll`
3. Builds a list of `ICommandHandler` implementations (`BrightnessCommandHandler`, `MonitorPowerCommandHandler`) — both use `CommandVocabulary` for shared parsing
4. Creates `VoiceListenerService` — builds one Vosk recognizer per culture from handlers' `SupportedCultures` and their `BuildVocabulary(culture)` output

### Vosk model setup

`VoskModelSetupService` (in `Services/`) checks for required models under `%LOCALAPPDATA%/WindowsAssistant/Models/{culture}/{folder}/`. If missing, it prompts the user, downloads the official `vosk-model-small-*` ZIPs and extracts them in-place. A directory is considered valid only if it contains the `am/`, `graph/`, and `conf/` subfolders.

Upgrading a model: drop a different Vosk model folder under the same `{culture}/` directory and restart the app. The service picks the first folder whose name is declared in `RequiredModels`.

### Adding a new voice command

1. Create a class implementing `ICommandHandler` in `Commands/`
2. Declare `SupportedCultures` (e.g. `[new("en-US"), new("pt-BR")]`)
3. `BuildVocabulary(CultureInfo culture)` returns the flat list of words this handler accepts for that culture (without the wake phrase) — reuse helpers in `CommandVocabulary`
4. `TryHandle(RecognitionOutput output)` parses `output.Text` with regex and executes the action, returning `CommandResult` or `null`
5. Register it in `TrayApplication.BuildHandlers()`
6. Update the help text in `UI/HelpDialog.cs`
7. Update `README.md` with the new command examples

### Key conventions

- `NativeMethods` (in `Infrastructure/`) centralizes all P/Invoke declarations — add new Win32 interop there
- Wake phrases per culture are defined in `VoiceListenerService.WakePhrases`
- Monitor indices are 1-based in voice commands, 0-based internally
- `CommandVocabulary` (in `Commands/`) centralizes all language words, ordinals, power states, and parsing — edit here to add new words or languages
- Brightness values 0–10 map to 0%–100% (×10); values 11–100 are direct percentages
- Monitor power uses VCP code 0xD6 via `SetVCPFeature` (1 = on, 4 = standby)
- Crash logs are written to `%LOCALAPPDATA%/WindowsAssistant/crash.log`
- Voice recognition events (including culture, text, confidence) are logged to `%LOCALAPPDATA%/WindowsAssistant/voice.log`
- Speech speed auto-adapts the minimum confidence threshold based on a rolling average of words-per-second (no timeout tuning — Vosk's VAD is internal)
