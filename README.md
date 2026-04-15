# Windows Assistant

Lightweight system tray app for Windows that controls your monitors by voice using the DDC/CI protocol.

## Features

- **Voice-controlled brightness** — say "Hey Windows, first 5" to set monitor 1 to 50%
- **Monitor power control** — turn individual monitors on/off (DPMS standby) by voice
- **Bilingual** — supports English and Portuguese (Brazil) commands simultaneously
- **Auto speech speed detection** — adapts to slow, normal, or fast speaking pace
- **DDC/CI** — works with any external monitor that supports the DDC/CI protocol
- **Startup with Windows** — optional, toggle via tray menu
- **Language dependency check** — detects missing speech language packs and offers to install them automatically
- **Help / Tutorial** — built-in guide accessible from the tray menu

## Voice Commands

### Brightness

| Example                                  | Effect          |
| ---------------------------------------- | --------------- |
| "Hey Windows, first 5"                   | Monitor 1 → 50% |
| "Hey Windows, monitor 1 50"              | Monitor 1 → 50% |
| "Hey Windows, both 3"                    | All → 30%       |
| "Ei Windows, primeiro 8"                 | Monitor 1 → 80% |
| "Ei Windows, ambos 5"                    | All → 50%       |
| "Hey Windows, brightness 5 on monitor 1" | Monitor 1 → 50% |
| "Ei Windows, brilho 3 no monitor 1"      | Monitor 1 → 30% |
| "Oi Windows, luminosidade 5 no segundo"  | Monitor 2 → 50% |
| "Olá Windows, luz 7 no terceiro"         | Monitor 3 → 70% |
| "Hey Windows, monitor 1 brightness 5"    | Monitor 1 → 50% |
| "Ei Windows, primeiro brilho 3"          | Monitor 1 → 30% |
| "Ei Windows, quarto luminosidade 8"      | Monitor 4 → 80% |

Values 0–10 are levels (×10). Values 11–100 are direct percentages.

### Monitor Power

| Example                           | Effect              |
| --------------------------------- | ------------------- |
| "Hey Windows, turn off monitor 1" | Monitor 1 → standby |
| "Hey Windows, first off"          | Monitor 1 → standby |
| "Hey Windows, enable second"      | Monitor 2 → on      |
| "Ei Windows, desligar monitor 1"  | Monitor 1 → standby |
| "Ei Windows, apaga primeiro"      | Monitor 1 → standby |
| "Ei Windows, acende segundo"      | Monitor 2 → on      |
| "Ei Windows, ligue terceiro"      | Monitor 3 → on      |
| "Ei Windows, primeiro desativar"  | Monitor 1 → standby |

Monitors: monitor 1–5, or ordinals (first/primeiro, second/segundo, third/terceiro, fourth/quarto, fifth/quinto).

Wake phrases (pt-BR): "Ei Windows", "Oi Windows", "Olá Windows".

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

## Futuras Features e Fixes

- **Alternativas à API do Windows de Text To Speech:** Explorar e implementar outras opções de reconhecimento de voz mais precisas ou flexíveis.
- **Melhorar o reconhecimento em Português:** Ajustar gramáticas, sensibilidade ou modelos para que comandos em português sejam reconhecidos com maior taxa de sucesso.
- **Melhorar as funcionalidades de ligar/desligar os monitores:** Aprimorar o controle de energia, suportando mais modelos de monitores e lidando melhor com cenários como múltiplos monitores e despertar (resume) do standby.
