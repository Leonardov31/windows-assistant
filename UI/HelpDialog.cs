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
          English:    "Hey Windows"
          Português:  "Ei Windows"


        QUICK COMMANDS
        --------------
        The fastest way to control your monitors.
        Values 1–10 are levels (×10). 0 or above 10
        are direct percentages.

          Brightness:
            "Hey Windows, first 2"       → monitor 1 at 20%
            "Hey Windows, second 50"     → monitor 2 at 50%
            "Ei Windows, primeiro 5"     → monitor 1 at 50%
            "Hey Windows, both 80"       → all at 80%
            "Ei Windows, ambos 3"        → all at 30%

          Power (on/off):
            "Hey Windows, first off"     → monitor 1 standby
            "Hey Windows, second on"     → monitor 2 on
            "Ei Windows, primeiro desligar" → standby
            "Ei Windows, segundo ligar"  → monitor 2 on

        Ordinals: first/primeiro, second/segundo,
        third/terceiro, fourth/quarto.
        All: both/all, ambos/todos.


        BRIGHTNESS (ALTERNATIVE FORMS)
        ------------------------------
        Short form:
          "Hey Windows, monitor 1 2"    → 20%
          "Hey Windows, monitor 1 20"   → 20%
          "Ei Windows, monitor 1 5"     → 50%

        Full form (scale 1–10 → 10%–100%):
          "Hey Windows, brightness 3 in monitor 1"
          "Ei Windows, brilho 5 no monitor 1"


        MONITOR POWER (ALTERNATIVE FORMS)
        ----------------------------------
          English:
            "Hey Windows, turn off monitor 1"
            "Hey Windows, turn on monitor 2"
            "Hey Windows, disable monitor 1"
            "Hey Windows, enable monitor 2"

          Português:
            "Ei Windows, desligar monitor 1"
            "Ei Windows, ligar monitor 2"

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
