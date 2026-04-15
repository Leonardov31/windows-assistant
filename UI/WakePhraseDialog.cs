using WindowsAssistant.Infrastructure;

namespace WindowsAssistant.UI;

/// <summary>
/// Small modal form that lets the user type a custom wake phrase.
/// Uses the same Fluent palette as the tray menu and dark title bar.
/// </summary>
internal sealed class WakePhraseDialog : Form
{
    private readonly TextBox _input;

    public string WakePhrase => _input.Text.Trim();

    internal WakePhraseDialog(string currentPhrase)
    {
        bool dark   = FluentMenuRenderer.IsSystemDark();
        var palette = new FluentMenuRenderer.FluentColors(dark);

        Text            = "Wake phrase";
        Size            = new Size(460, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;
        BackColor       = palette.Background;
        ForeColor       = palette.Foreground;
        Font            = PickFont();
        Padding         = new Padding(24, 20, 24, 20);

        HandleCreated += (_, _) =>
        {
            ApplyDarkTitleBar(Handle, dark);
            ApplyRoundedCorners(Handle);
        };

        var title = new Label
        {
            Text      = "Choose your wake phrase",
            AutoSize  = true,
            Font      = new Font(Font.FontFamily, 14f, FontStyle.Bold),
            ForeColor = palette.Foreground,
            Location  = new Point(24, 20),
        };
        Controls.Add(title);

        var hint = new Label
        {
            Text      = "Type the word or short phrase the app should listen for.\n" +
                        "Use something the speech model knows (common dictionary words).",
            AutoSize  = true,
            MaximumSize = new Size(400, 0),
            ForeColor = dark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(90, 90, 90),
            Location  = new Point(24, 52),
        };
        Controls.Add(hint);

        _input = new TextBox
        {
            Text       = currentPhrase,
            Width      = 400,
            Location   = new Point(24, 100),
            Font       = new Font(Font.FontFamily, 12f),
            BackColor  = palette.ItemHover,
            ForeColor  = palette.Foreground,
            BorderStyle = BorderStyle.FixedSingle,
        };
        Controls.Add(_input);

        var ok = new Button
        {
            Text      = "Save",
            Width     = 100,
            Height    = 32,
            Location  = new Point(308, 138),
            BackColor = palette.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_input.Text)) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(ok);

        var cancel = new Button
        {
            Text      = "Cancel",
            Width     = 92,
            Height    = 32,
            Location  = new Point(210, 138),
            BackColor = palette.ItemHover,
            ForeColor = palette.Foreground,
            FlatStyle = FlatStyle.Flat,
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Font PickFont()
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

    private static void ApplyDarkTitleBar(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int value = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch { }
    }

    private static void ApplyRoundedCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int pref = (int)NativeMethods.DwmWindowCornerPreference.Round;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch { }
    }
}
