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

`VoiceListenerService` creates one `SpeechRecognitionEngine` per culture (en-US, pt-BR). Each engine runs in parallel with its own wake phrase:
- en-US: "hey windows"
- pt-BR: "ei windows"

If a language pack isn't installed, that engine is silently skipped.

### Service wiring

`TrayApplication` (in `UI/`) owns the lifecycle. Its constructor:
1. Runs `LanguageSetupService.CheckAndPromptInstall()` — checks for missing speech language packs and prompts installation
2. Creates `MonitorControlService` — enumerates physical monitors, exposes DDC/CI brightness get/set via P/Invoke to `dxva2.dll`
3. Builds a list of `ICommandHandler` implementations (`BrightnessCommandHandler`, `MonitorPowerCommandHandler`) — both use `CommandVocabulary` for shared parsing
4. Creates `VoiceListenerService` — creates one engine per culture from handlers' `SupportedCultures`

### Language dependency check

`LanguageSetupService` (in `Services/`) checks which speech recognition cultures are installed via `SpeechRecognitionEngine.InstalledRecognizers()`. If any required culture (en-US, pt-BR) is missing, it shows a prompt and runs `Add-WindowsCapability` via elevated PowerShell to install the language pack.

### Adding a new voice command

1. Create a class implementing `ICommandHandler` in `Commands/`
2. Declare `SupportedCultures` (e.g. `[new("en-US"), new("pt-BR")]`)
3. `BuildGrammar(CultureInfo culture)` returns the grammar fragment per language (without wake phrase)
4. `TryHandle()` parses `RecognitionResult.Text` and executes the action, returning `CommandResult` or `null`
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
- Speech speed auto-adapts based on rolling average of words-per-second
