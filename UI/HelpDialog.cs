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

        WAKE PHRASES
          English:    "Hey Windows" (disabled in this build)
          Português:  "Ei Computador" / "Oi Computador" / "Olá Computador"

        Tip: números devem ser falados por extenso
          ex: "Ei Computador, brilho cinquenta no primeiro"


        BRIGHTNESS CONTROL
        ------------------
        Controls monitor brightness via DDC/CI.
        Values 0–10 are levels (×10). Above 10 = direct %.

        Short form:
          "Hey Windows, first 5"             → monitor 1 at 50%
          "Hey Windows, monitor 1 50"        → monitor 1 at 50%
          "Hey Windows, both 3"              → all monitors at 30%
          "Ei Windows, primeiro 8"           → monitor 1 at 80%
          "Ei Windows, ambos 5"              → all monitors at 50%

        Long form 1:
          "Hey Windows, brightness 5 on monitor 1"
          "Ei Windows, brilho 3 no monitor 1"
          "Ei Windows, luminosidade 5 no segundo"
          "Ei Windows, luz 7 no terceiro"
          "Hey Windows, brightness 5 on first"

        Long form 2:
          "Hey Windows, monitor 1 brightness 5"
          "Ei Windows, primeiro brilho 3"
          "Ei Windows, quarto luminosidade 8"

        Monitors: monitor 1–5, or ordinals:
          first/primeiro, second/segundo,
          third/terceiro, fourth/quarto,
          fifth/quinto.
        All: both/all, ambos/todos.
        Brightness keyword (pt-BR):
          brilho / luminosidade / luz.


        MONITOR POWER (ON / OFF)
        ------------------------
        Puts a monitor into standby or wakes it.
        Single monitor only.

          "Hey Windows, turn off monitor 1"
          "Hey Windows, enable first"
          "Hey Windows, first off"
          "Ei Windows, desligar monitor 1"
          "Ei Windows, apaga o primeiro" (see note*)
          "Ei Windows, acende segundo"
          "Ei Windows, ligue terceiro"
          "Ei Windows, primeiro desativar"

        *Grammar does not include articles; say
        "apaga primeiro" (not "apaga o primeiro").

        Power on:  on, enable, turn on,
                   ligar, liga, ligue, ativar,
                   acender, acende, acenda
        Power off: off, disable, turn off,
                   desligar, desliga, desligue,
                   desativar, apagar, apaga, apague

        Note: to wake a monitor by voice, your
        microphone must not be on the sleeping monitor.


        SPEECH SPEED
        ------------
        The app automatically detects your speaking pace
        (Slow / Normal / Fast) and adjusts recognition
        accordingly. You can also set it manually via the
        tray icon menu → Speech speed.


        LANGUAGE SETUP
        --------------
        On startup the app checks whether each required
        speech language pack is installed. If any are
        missing it will offer to install them automatically
        (requires administrator privileges).

        You can also install them manually:
          Settings → Time & Language → Language
          → Add a language → enable Speech recognition


        TIPS
        ----
        • Monitors must support DDC/CI (most external
          monitors do; laptop built-in screens usually don't).
        • Right-click the tray icon for all options.
        """;

    // =========================================================================

    internal HelpDialog()
    {
        Text            = "Windows Assistant — Help";
        Size            = new Size(500, 480);
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
