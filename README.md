# Windows Assistant

Lightweight system tray app for Windows that controls your monitors by voice using the DDC/CI protocol.

## Features

- **Voice-controlled brightness** — say "Hey Windows, monitor 1 20" to set 20% brightness
- **Monitor power control** — turn monitors on/off (DPMS standby) by voice
- **Bilingual** — supports English and Portuguese commands simultaneously
- **Auto speech speed detection** — adapts to slow, normal, or fast speaking pace
- **DDC/CI** — works with any external monitor that supports the DDC/CI protocol
- **Startup with Windows** — optional, toggle via tray menu
- **Language dependency check** — detects missing speech language packs and offers to install them automatically
- **Help / Tutorial** — built-in guide accessible from the tray menu

## Voice Commands

### Quick Commands

The fastest way to control your monitors. Values 1–10 are levels (×10), 0 or above 10 are direct percentages.

| Example | Effect |
|---|---|
| "Hey Windows, first 2" | Monitor 1 → 20% |
| "Hey Windows, second 50" | Monitor 2 → 50% |
| "Ei Windows, primeiro 5" | Monitor 1 → 50% |
| "Hey Windows, both 80" | All → 80% |
| "Hey Windows, first off" | Monitor 1 → standby |
| "Hey Windows, both on" | All → on |
| "Ei Windows, primeiro desligar" | Monitor 1 → standby |
| "Ei Windows, todos ligar" | All → on |

Ordinals: first/primeiro, second/segundo, third/terceiro, fourth/quarto.
All monitors: both/all, ambos/todos.

### Brightness (alternative forms)

| Example | Effect |
|---|---|
| "Hey Windows, monitor 1 2" | Monitor 1 → 20% |
| "Hey Windows, monitor 1 20" | Monitor 1 → 20% |
| "Hey Windows, brightness 3 in monitor 1" | Monitor 1 → 30% |
| "Ei Windows, brilho 5 no monitor 1" | Monitor 1 → 50% |

### Monitor Power (alternative forms)

| Example | Effect |
|---|---|
| "Hey Windows, turn off monitor 1" | Monitor 1 → standby |
| "Hey Windows, turn on monitor 2" | Monitor 2 → on |
| "Hey Windows, disable monitor 1" | Monitor 1 → standby |
| "Hey Windows, turn off all monitors" | All → standby |
| "Ei Windows, desligar monitor 1" | Monitor 1 → standby |
| "Ei Windows, ligar monitor 2" | Monitor 2 → on |

## Requirements

- Windows 10/11
- .NET 10 Runtime (or use the self-contained build)
- External monitor with DDC/CI support
- For Portuguese: the app will detect and offer to install the pt-BR language pack automatically on startup

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
