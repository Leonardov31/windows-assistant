# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                          # debug build
dotnet build -c Release               # release build
dotnet run                            # run (appears in system tray, no console window)
```

### Publish

```bash
# Framework-dependent (small, requires .NET 10 on host)
dotnet publish WindowsAssistant.csproj -c Release -r win-x64 --self-contained false -o ./publish

# Self-contained single-file with compression
dotnet publish WindowsAssistant.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish-compressed
```

Note: trimming (`PublishTrimmed`) is incompatible with Windows Forms.

## Architecture

This is a Windows-only system tray app (.NET 10 / WinForms) that listens for voice commands and controls monitors via DDC/CI.

### Service wiring

`TrayApplication` (in `UI/`) owns the lifecycle. Its constructor:
1. Creates `MonitorControlService` — enumerates physical monitors, exposes DDC/CI brightness get/set via P/Invoke to `dxva2.dll`
2. Builds a list of `ICommandHandler` implementations (currently just `BrightnessCommandHandler`)
3. Creates `VoiceListenerService` — builds a combined `System.Speech` grammar from all handlers, listens continuously for the wake phrase "Hey Windows" + command

### Adding a new voice command

1. Create a class implementing `ICommandHandler` in `Commands/`
2. `BuildGrammar()` returns the grammar fragment (without the wake phrase)
3. `TryHandle()` parses the `RecognitionResult.Text` and executes the action, returning `CommandResult` or `null`
4. Register it in `TrayApplication.BuildHandlers()`

### Key conventions

- `NativeMethods` (in `Infrastructure/`) centralizes all P/Invoke declarations — add new Win32 interop there
- Voice grammar uses `System.Speech.Recognition.GrammarBuilder`; the wake phrase "hey windows" is prepended automatically by `VoiceListenerService`
- Monitor indices are 1-based in voice commands, 0-based internally
- Brightness scale in voice: 1–10 maps to 10%–100% (level × 10)
