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


        BRIGHTNESS CONTROL
        ------------------
        Changes monitor brightness via DDC/CI.

        Full form (scale 1–10 → 10%–100%):
          "Hey Windows, brightness 3 in monitor 1"
          "Hey Windows, brightness 7 on monitor 2"
          "Ei Windows, brilho 5 no monitor 1"

        Short form (level 1–10 or percentage):
          "Hey Windows, monitor 1 2"    → 20%
          "Hey Windows, monitor 1 20"   → 20%
          "Hey Windows, monitor 2 100"  → 100%
          "Ei Windows, monitor 1 5"     → 50%

        Values 1–10 are treated as levels (×10).
        Values 0 or above 10 are direct percentages.


        MONITOR POWER (ON / OFF)
        ------------------------
        Puts monitors into DPMS standby or wakes them.
        Works with any DDC/CI monitor.

          English:
            "Hey Windows, turn off monitor 1"
            "Hey Windows, turn on monitor 2"
            "Hey Windows, disable monitor 1"
            "Hey Windows, enable monitor 2"
            "Hey Windows, turn off all monitors"
            "Hey Windows, turn on monitor"  (all)

          Português:
            "Ei Windows, desligar monitor 1"
            "Ei Windows, ligar monitor 2"
            "Ei Windows, desligar todos monitor"
            "Ei Windows, ligar monitor"  (todos)

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
