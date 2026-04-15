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
            Speech recognition uses Windows' built-in engine
            (Windows.Media.SpeechRecognition). Make sure the language pack
            for the selected language is installed under Settings →
            Time & language → Language.

            Two-phase flow:
              1. Say the wake phrase. A chime confirms it was heard and
                 anything you said before it is discarded.
              2. Within 5 seconds, say the command (up to 6 words). You
                 can also say wake phrase + command in one breath.

            Numbers are spoken as words (cinco, fifty) — not digits.
            """),

        ("Wake phrase", """
            The wake phrase is user-defined — set it from the tray menu
            (Wake phrase... → type any word or short sentence). Default is
            "computador". Use words the selected speech model knows; if a
            phrase never triggers, try a common dictionary word instead.

            Only one language is active at a time. Switch between Português
            and English from the tray menu → Language.
            """),

        ("Brightness control", """
            Values 0–10 are levels (×10). 20, 30, ..., 100 are direct percentages.

            Short form
              {wake} primeiro cinco         →  M1 at 50%
              {wake} monitor um cinquenta   →  M1 at 50%
              {wake} ambos três             →  all at 30%
              {wake} first five             →  M1 at 50%
              {wake} both fifty             →  all at 50%

            Long form — keyword + value + monitor
              {wake} brilho cinco no primeiro
              {wake} luminosidade oito no segundo
              {wake} luz sete no terceiro
              {wake} brightness fifty on first
              {wake} brightness five on monitor two

            Long form — monitor + keyword + value
              {wake} primeiro brilho três
              {wake} quarto luminosidade oito
              {wake} monitor one brightness five

            Natural phrasings
              {wake} set monitor one to 50
              {wake} put first at 80
              {wake} adjust second to 70
              {wake} change third to 40
              {wake} ajusta primeiro em 30
              {wake} coloca segundo em 80
              {wake} deixa quarto em 20
              {wake} 50 no primeiro
              {wake} 80 on monitor one

            No target — applies to ALL monitors
              {wake} brilho 30              →  all at 30%
              {wake} brilho em 30           →  all at 30%
              {wake} luz 2                  →  all at 20%
              {wake} brightness 50          →  all at 50%
              {wake} brightness in 50       →  all at 50%
              {wake} light 30               →  all at 30%

            Monitors:       monitor um..cinco  /  monitor one..five
            Ordinals:       primeiro..quinto   /  first..fifth
            All:            ambos, todos       /  both, all
            Brightness:     brilho, luminosidade, luz  /  brightness, light
            Set verbs:      ajustar, coloca, define, deixa, muda, pôr
                            set, put, make, change, adjust

            Replace {wake} above with whatever you set in the tray menu.
            """),

        ("Monitor power (on / off)", """
            Puts a monitor into standby or wakes it — one monitor per command.

              {wake} desligar monitor um
              {wake} apaga primeiro
              {wake} acende segundo
              {wake} ligue terceiro
              {wake} turn off monitor one
              {wake} enable first
              {wake} first off

            Natural phrasings
              {wake} power off monitor one
              {wake} shut down monitor one
              {wake} sleep monitor one
              {wake} wake up first
              {wake} power on second
              {wake} acorda o primeiro
              {wake} desperta o segundo
              {wake} dormir o primeiro
              {wake} adormecer o terceiro

            Power on:   ligar, liga, ligue, ativar, acender, acende, acenda,
                        acorda, acorde, acordar, desperta, desperte, despertar
                        on, enable, turn on, power on, wake, wake up
            Power off:  desligar, desliga, desligue, desativar,
                        apagar, apaga, apague,
                        dormir, dorme, durma, adormecer, adormece, adormeça
                        off, disable, turn off, power off, shut down, shut off, sleep
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
            Language       —  pick pt-BR or en-US; only one is active at
                              a time. Requires the matching Windows speech
                              language pack to be installed.
            Wake phrase... —  open a dialog to type any word or sentence
                              the app should listen for.
            Monitors       —  click to list the detected displays in a balloon.
            Refresh        —  re-enumerate displays after plugging one in/out.
            Start with Windows  —  optional autostart on logon.
            """),

        ("Tips", """
            •  You can say wake phrase + command in one breath, or wake
               first, wait for the chime, then speak the command.
            •  Commands are capped at 6 words after the wake phrase.
            •  If no command arrives within 5 seconds after the wake
               chime, the assistant quietly goes back to listening for
               the wake phrase.
            •  Do NOT use articles ("o", "a") with the older verbs. Say
               "apaga primeiro" — not "apaga o primeiro". Natural-language
               verbs (ajusta, acorda, adormecer) do accept an article.
            •  If the monitor hosting the mic goes to standby, voice control
               stops until it is woken manually.
            •  Monitors must support DDC/CI. External panels usually do; laptop
               built-in screens rarely do.
            •  Both the terminal and %LOCALAPPDATA%\\WindowsAssistant\\voice.log
               stay silent until the wake phrase is detected — unrelated
               speech is discarded without a trace.
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
