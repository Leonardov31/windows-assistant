# Windows Assistant

Lightweight system tray app for Windows that controls your monitors by voice using the DDC/CI protocol.

## Features

- **Voice-controlled brightness** — say "Hey Windows, brightness 3 in monitor 1" to set 30% brightness
- **Bilingual** — supports English and Portuguese commands simultaneously
- **Auto speech speed detection** — adapts to slow, normal, or fast speaking pace
- **DDC/CI** — works with any external monitor that supports the DDC/CI protocol
- **Startup with Windows** — optional, toggle via tray menu
- **Help / Tutorial** — built-in guide accessible from the tray menu

## Voice Commands

### Brightness

| Language | Example | Effect |
|---|---|---|
| English | "Hey Windows, brightness 3 in monitor 1" | Monitor 1 → 30% |
| English | "Hey Windows, brightness 10 on monitor 2" | Monitor 2 → 100% |
| Português | "Ei Windows, brilho 5 no monitor 1" | Monitor 1 → 50% |
| Português | "Ei Windows, brilho 8 do monitor 2" | Monitor 2 → 80% |

Scale: 1–10 maps to 10%–100%.

## Requirements

- Windows 10/11
- .NET 10 Runtime (or use the self-contained build)
- External monitor with DDC/CI support
- For Portuguese: install the pt-BR language pack with speech recognition in Windows Settings

## Build

```bash
# Debug
dotnet build WindowsAssistant.csproj

# Self-contained single-file (no .NET required on host)
dotnet publish WindowsAssistant.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./dist
```

## Usage

1. Run `WindowsAssistant.exe`
2. A blue "W" icon appears in the system tray
3. Right-click for options (Help, Monitors, Speech speed, Startup, Exit)
4. Speak commands using the wake phrase + command
