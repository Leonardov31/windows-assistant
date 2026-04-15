using WindowsAssistant.Infrastructure;

namespace WindowsAssistant.UI;

/// <summary>
/// Scrollable help window listing every available voice command.
/// Styled to match the Fluent tray menu: dark title bar, Segoe UI
/// typography, sectioned layout with clear heading hierarchy.
/// Add or remove sections by editing <see cref="Sections"/>.
/// </summary>
internal sealed class HelpDialog : Form
{
    // =========================================================================
    // CONTENT — edit this list to update the tutorial
    // =========================================================================

    private static readonly (string Title, string Body)[] Sections =
    {
        ("How it works", """
            Speech recognition runs offline via Vosk. Every utterance must start
            with a wake phrase, followed by the command, all in one breath.
            Numbers are spoken as words (cinco, fifty) — not digits.
            """),

        ("Wake phrases", """
            Português:   Ei Computador   •   Oi Computador   •   Olá Computador
            English:     Hey Windows     •   Hey Computer
            """),

        ("Brightness control", """
            Values 0–10 are levels (×10). 20, 30, ..., 100 are direct percentages.

            Short form
              Ei Computador primeiro cinco         →  M1 at 50%
              Ei Computador monitor um cinquenta   →  M1 at 50%
              Ei Computador ambos três             →  all at 30%
              Hey Windows first five               →  M1 at 50%
              Hey Windows both fifty               →  all at 50%

            Long form — keyword + value + monitor
              Ei Computador brilho cinco no primeiro
              Oi Computador luminosidade oito no segundo
              Olá Computador luz sete no terceiro
              Hey Windows brightness fifty on first
              Hey Windows brightness five on monitor two

            Long form — monitor + keyword + value
              Ei Computador primeiro brilho três
              Ei Computador quarto luminosidade oito
              Hey Windows monitor one brightness five

            Monitors:       monitor um..cinco  /  monitor one..five
            Ordinals:       primeiro..quinto   /  first..fifth
            All:            ambos, todos       /  both, all
            Brightness:     brilho, luminosidade, luz  /  brightness
            """),

        ("Monitor power (on / off)", """
            Puts a monitor into standby or wakes it — one monitor per command.

              Ei Computador desligar monitor um
              Ei Computador apaga primeiro
              Oi Computador acende segundo
              Olá Computador ligue terceiro
              Hey Windows turn off monitor one
              Hey Windows enable first
              Hey Windows first off

            Power on:   ligar, liga, ligue, ativar, acender, acende, acenda
                        on, enable, turn on
            Power off:  desligar, desliga, desligue, desativar,
                        apagar, apaga, apague
                        off, disable, turn off
            """),

        ("Number words", """
            Português:  zero, um, dois, três, quatro, cinco, seis, sete, oito,
                        nove, dez, vinte, trinta, quarenta, cinquenta, sessenta,
                        setenta, oitenta, noventa, cem
            English:    zero, one, two, three, four, five, six, seven, eight,
                        nine, ten, twenty, thirty, forty, fifty, sixty, seventy,
                        eighty, ninety, hundred
            """),

        ("Tray menu", """
            Languages      —  toggle pt-BR / en-US at runtime. Disabling a
                              language frees ~130 MB of RAM used by the Vosk model.
            Monitors       —  click to list the detected displays in a balloon.
            Refresh        —  re-enumerate displays after plugging one in/out.
            Start with Windows  —  optional autostart on logon.
            """),

        ("Tips", """
            •  Speak wake phrase + command as ONE utterance, no pause.
            •  Do NOT use articles ("o", "a"). Say "apaga primeiro" — not
               "apaga o primeiro".
            •  If the monitor hosting the mic goes to standby, voice control
               stops until it is woken manually.
            •  Monitors must support DDC/CI. External panels usually do; laptop
               built-in screens rarely do.
            •  To debug recognition, open %LOCALAPPDATA%\\WindowsAssistant\\
               voice.log. Every transcription is there with the drop reason.
            """),
    };

    // =========================================================================

    internal HelpDialog()
    {
        bool dark      = FluentMenuRenderer.IsSystemDark();
        var  palette   = new FluentMenuRenderer.FluentColors(dark);
        var  background = palette.Background;
        var  foreground = palette.Foreground;
        var  muted     = dark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(90, 90, 90);

        Text            = "Windows Assistant — Help";
        Size            = new Size(620, 720);
        MinimumSize     = new Size(480, 500);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;
        BackColor       = background;
        ForeColor       = foreground;
        Font            = PickBodyFont();
        Padding         = new Padding(24, 20, 24, 20);

        HandleCreated += (_, _) =>
        {
            ApplyDarkTitleBar(Handle, dark);
            ApplyRoundedCorners(Handle);
        };

        var scroll = new Panel
        {
            Dock         = DockStyle.Fill,
            AutoScroll   = true,
            BackColor    = background,
        };
        Controls.Add(scroll);

        var content = new FlowLayoutPanel
        {
            FlowDirection  = FlowDirection.TopDown,
            WrapContents   = false,
            AutoSize       = true,
            AutoSizeMode   = AutoSizeMode.GrowAndShrink,
            BackColor      = background,
            Padding        = new Padding(4, 4, 24, 4), // right padding leaves room for scrollbar
        };
        scroll.Controls.Add(content);

        // Title -------------------------------------------------------------
        content.Controls.Add(BuildTitle("Voice commands", foreground));
        content.Controls.Add(BuildSubtitle(
            "Say a wake phrase, then the command — all in one breath.", muted));
        content.Controls.Add(Spacer(14));

        // Sections ----------------------------------------------------------
        foreach (var (title, body) in Sections)
        {
            content.Controls.Add(BuildSectionHeader(title, foreground));
            content.Controls.Add(BuildSectionBody(body.TrimEnd(), foreground));
            content.Controls.Add(Spacer(10));
        }

        // Resize body labels when the form width changes -------------------
        scroll.Resize += (_, _) =>
        {
            int w = scroll.ClientSize.Width - content.Padding.Horizontal;
            foreach (Control c in content.Controls)
                if (c is Label l && l.Tag is string tag && tag == "body")
                    l.MaximumSize = new Size(w, 0);
        };
    }

    // -------------------------------------------------------------------------
    // Section builders
    // -------------------------------------------------------------------------

    private Label BuildTitle(string text, Color fg) => new()
    {
        Text      = text,
        AutoSize  = true,
        Font      = new Font(PickBodyFont().FontFamily, 18f, FontStyle.Bold),
        ForeColor = fg,
        Margin    = new Padding(0, 0, 0, 2),
    };

    private Label BuildSubtitle(string text, Color fg) => new()
    {
        Text      = text,
        AutoSize  = true,
        Font      = new Font(PickBodyFont().FontFamily, 10f, FontStyle.Regular),
        ForeColor = fg,
        Margin    = new Padding(0, 0, 0, 0),
    };

    private Label BuildSectionHeader(string text, Color fg) => new()
    {
        Text      = text,
        AutoSize  = true,
        Font      = new Font(PickBodyFont().FontFamily, 12f, FontStyle.Bold),
        ForeColor = fg,
        Margin    = new Padding(0, 8, 0, 6),
    };

    private Label BuildSectionBody(string text, Color fg) => new()
    {
        Text        = text,
        AutoSize    = true,
        Font        = PickMonoFont(),
        ForeColor   = fg,
        Margin      = new Padding(0),
        Tag         = "body",
        MaximumSize = new Size(560, 0),
    };

    private static Control Spacer(int height) => new Panel { Height = height, Width = 1 };

    // -------------------------------------------------------------------------
    // Fonts
    // -------------------------------------------------------------------------

    private static Font PickBodyFont()
    {
        foreach (var family in new[] { "Segoe UI Variable Text", "Segoe UI Variable", "Segoe UI" })
        {
            try
            {
                var f = new Font(family, 10f);
                if (f.Name == family) return f;
                f.Dispose();
            }
            catch { /* try next */ }
        }
        return SystemFonts.MessageBoxFont ?? new Font("Tahoma", 10f);
    }

    /// <summary>
    /// Monospace font for the body of each section: column alignment in the
    /// examples ("→ M1 at 50%") only looks right when characters have the
    /// same advance width. Cascadia Mono is the modern Win11 default;
    /// Consolas is the universal fallback.
    /// </summary>
    private static Font PickMonoFont()
    {
        foreach (var family in new[] { "Cascadia Mono", "Cascadia Code", "Consolas", "Courier New" })
        {
            try
            {
                var f = new Font(family, 9.5f);
                if (f.Name == family) return f;
                f.Dispose();
            }
            catch { /* try next */ }
        }
        return new Font(FontFamily.GenericMonospace, 9.5f);
    }

    // -------------------------------------------------------------------------
    // DWM helpers — dark title bar + rounded corners on Win11
    // -------------------------------------------------------------------------

    private static void ApplyDarkTitleBar(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int value = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value,
                sizeof(int));
        }
        catch
        {
            // Pre-Win10 20H1 doesn't support this attribute — ignore
        }
    }

    private static void ApplyRoundedCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int pref = (int)NativeMethods.DwmWindowCornerPreference.Round;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref pref,
                sizeof(int));
        }
        catch
        {
            // Pre-Win11 — ignore
        }
    }
}
