namespace WindowsAssistant.UI;

/// <summary>
/// Shows a scrollable tutorial/help window listing every available voice command.
/// Update <see cref="HelpContent"/> whenever a new feature is added.
/// </summary>
internal sealed class HelpDialog : Form
{
    // =========================================================================
    // HELP CONTENT — update this section when adding new features
    // =========================================================================

    private const string HelpContent = """
        WINDOWS ASSISTANT — VOICE COMMANDS
        ===================================

        HOW IT WORKS
        ------------
        Speech recognition runs offline via Vosk. Each
        utterance must start with a wake phrase, followed
        by the command, all in one breath. Numbers are
        spoken as words (cinco, fifty), not digits.

        WAKE PHRASES
          Português:  "Ei Computador"
                      "Oi Computador"
                      "Olá Computador"
          English:    "Hey Windows"
                      "Hey Computer"


        BRIGHTNESS CONTROL
        ------------------
        Controls monitor brightness via DDC/CI.
        Values 0–10 are levels (×10). 20, 30, ..., 100
        are direct percentages.

        Short form:
          "Ei Computador primeiro cinco"      → M1 at 50%
          "Ei Computador monitor um cinquenta" → M1 at 50%
          "Ei Computador ambos três"          → all at 30%
          "Hey Windows first five"            → M1 at 50%
          "Hey Windows both fifty"            → all at 50%

        Long form — keyword + value + monitor:
          "Ei Computador brilho cinco no primeiro"
          "Oi Computador luminosidade oito no segundo"
          "Olá Computador luz sete no terceiro"
          "Hey Windows brightness fifty on first"
          "Hey Windows brightness five on monitor two"

        Long form — monitor + keyword + value:
          "Ei Computador primeiro brilho três"
          "Ei Computador quarto luminosidade oito"
          "Hey Windows monitor one brightness five"

        Monitors: monitor um..cinco / monitor one..five
          or ordinals:
            primeiro / segundo / terceiro / quarto / quinto
            first / second / third / fourth / fifth
        All: ambos, todos / both, all
        Brightness (pt-BR): brilho / luminosidade / luz


        MONITOR POWER (ON / OFF)
        ------------------------
        Puts a monitor into standby or wakes it. Single
        monitor per command.

          "Ei Computador desligar monitor um"
          "Ei Computador apaga primeiro"
          "Oi Computador acende segundo"
          "Olá Computador ligue terceiro"
          "Ei Computador primeiro desativar"
          "Hey Windows turn off monitor one"
          "Hey Windows enable first"
          "Hey Windows first off"

        Power on:  ligar / liga / ligue / ativar /
                   acender / acende / acenda
                   on / enable / turn on
        Power off: desligar / desliga / desligue /
                   desativar / apagar / apaga / apague
                   off / disable / turn off


        NUMBER WORDS
        ------------
        Português:  zero, um, dois, três, quatro, cinco,
                    seis, sete, oito, nove, dez,
                    vinte, trinta, quarenta, cinquenta,
                    sessenta, setenta, oitenta, noventa, cem
        English:    zero, one, two, three, four, five,
                    six, seven, eight, nine, ten,
                    twenty, thirty, forty, fifty, sixty,
                    seventy, eighty, ninety, hundred


        TRAY MENU
        ---------
        • Languages      — toggle pt-BR / en-US at runtime.
                           Disabling a language frees ~130 MB
                           of RAM used by its Vosk model.
        • Speech speed   — adjusts confidence threshold
                           (Slow / Normal / Fast). Auto-detects
                           your pace if left on Normal.
        • Monitors       — click to list detected displays.
        • Start with Windows — optional autostart on logon.


        TIPS
        ----
        • Speak the wake phrase + command as ONE utterance,
          no pause in the middle.
        • Do NOT use articles ("o", "a"). Say "apaga primeiro"
          not "apaga o primeiro".
        • If the monitor with the mic goes to standby, voice
          control stops working until you wake it up manually.
        • Monitors must support DDC/CI — most external
          monitors do; laptop built-in screens rarely do.
        • Check %LOCALAPPDATA%\\WindowsAssistant\\voice.log
          to see what the recognizer transcribed.
        """;

    // =========================================================================

    internal HelpDialog()
    {
        Text            = "Windows Assistant — Help";
        Size            = new Size(520, 640);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;

        var textBox = new TextBox
        {
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Vertical,
            Dock       = DockStyle.Fill,
            Font       = new Font("Consolas", 9.5f),
            BackColor  = Color.FromArgb(30, 30, 30),
            ForeColor  = Color.FromArgb(220, 220, 220),
            Text       = HelpContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
        };

        Controls.Add(textBox);
    }
}
