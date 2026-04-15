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
        Scale: 1–10 (maps to 10%–100%).

          English:
            "Hey Windows, brightness 3 in monitor 1"
            "Hey Windows, brightness 7 on monitor 2"
            "Hey Windows, brightness 10 monitor 1"

          Português:
            "Ei Windows, brilho 3 no monitor 1"
            "Ei Windows, brilho 5 do monitor 2"
            "Ei Windows, brilho 8 monitor 1"


        SPEECH SPEED
        ------------
        The app automatically detects your speaking pace
        (Slow / Normal / Fast) and adjusts recognition
        accordingly. You can also set it manually via the
        tray icon menu → Speech speed.


        TIPS
        ----
        • Monitors must support DDC/CI (most external
          monitors do; laptop built-in screens usually don't).
        • For Portuguese recognition, install the pt-BR
          language pack with speech support in Windows
          Settings → Language.
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
