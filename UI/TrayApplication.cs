using Microsoft.Win32;
using WindowsAssistant.Commands;
using WindowsAssistant.Infrastructure;
using WindowsAssistant.Services;

namespace WindowsAssistant.UI;

/// <summary>
/// Main application context — runs invisibly in the system tray.
/// Owns all services and wires them together.
///
/// To add a new feature: create an <see cref="ICommandHandler"/> and
/// register it in <see cref="BuildHandlers"/>.
/// </summary>
public sealed class TrayApplication : ApplicationContext, IDisposable
{
    private readonly MonitorControlService _monitorService;
    private readonly VoiceListenerService  _voiceService;
    private readonly NotifyIcon            _trayIcon;
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _monitorsItem;
    private bool _disposed;

    public TrayApplication()
    {
        // Make sure the Vosk speech models are present before wiring the listener
        VoskModelSetupService.EnsureModelsAvailable();

        _monitorService = new MonitorControlService();
        _monitorService.RefreshMonitors();

        // Re-enumerate when the user connects/disconnects a display
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _voiceService = new VoiceListenerService(BuildHandlers());
        _voiceService.CommandExecuted += OnCommandExecuted;
        _voiceService.Start();

        _trayIcon = BuildTrayIcon();

        var langs = string.Join(", ", _voiceService.ActiveCultures);
        _trayIcon.ShowBalloonTip(3000, "Windows Assistant", $"Listening ({langs})", ToolTipIcon.Info);
    }

    // -------------------------------------------------------------------------
    // Handler registry — add new ICommandHandler instances here
    // -------------------------------------------------------------------------

    private List<ICommandHandler> BuildHandlers() =>
    [
        new BrightnessCommandHandler(_monitorService),
        new MonitorPowerCommandHandler(_monitorService),
    ];

    // -------------------------------------------------------------------------
    // Tray icon
    // -------------------------------------------------------------------------

    private NotifyIcon BuildTrayIcon()
    {
        var renderer = new FluentMenuRenderer();
        var colors   = (FluentMenuRenderer.FluentColors)renderer.ColorTable;
        var menuFont = PickMenuFont();

        var menu = new ContextMenuStrip
        {
            ShowImageMargin  = true,
            ShowCheckMargin  = false,
            RenderMode       = ToolStripRenderMode.Professional,
            Renderer         = renderer,
            BackColor        = colors.Background,
            ForeColor        = colors.Foreground,
            Font             = menuFont,
            Padding          = new Padding(4),
        };
        menu.HandleCreated += (_, _) => ApplyRoundedCorners(menu.Handle);

        // --- Header -------------------------------------------------------
        var header = new ToolStripLabel("Windows Assistant")
        {
            Font      = new Font(menuFont, FontStyle.Bold),
            ForeColor = colors.Foreground,
            Enabled   = false,
            Padding   = new Padding(8, 6, 8, 2),
        };
        menu.Items.Add(header);

        _statusItem = new ToolStripMenuItem(BuildStatusText())
        {
            Enabled = false,
            ForeColor = colors.Foreground,
            Padding = new Padding(0, 0, 0, 4),
        };
        menu.Items.Add(_statusItem);

        menu.Items.Add(new ToolStripSeparator());

        // --- Actions ------------------------------------------------------
        menu.Items.Add(MakeItem("Help / Tutorial", FluentIcon.Help, colors, (_, _) => ShowHelp()));

        _monitorsItem = MakeItem(BuildMonitorsText(), FluentIcon.Monitor, colors, OnShowMonitorInfo);
        menu.Items.Add(_monitorsItem);

        menu.Items.Add(MakeItem("Refresh monitors", FluentIcon.Refresh, colors, (_, _) => RefreshMonitors()));

        menu.Items.Add(new ToolStripSeparator());

        // --- Voice settings -----------------------------------------------
        var languagesMenu = BuildLanguagesMenu();
        languagesMenu.Image = RenderGlyph(FluentIcon.Globe, colors.Foreground);
        menu.Items.Add(languagesMenu);

        var speedMenu = BuildSpeedMenu();
        speedMenu.Image = RenderGlyph(FluentIcon.Microphone, colors.Foreground);
        menu.Items.Add(speedMenu);

        menu.Items.Add(new ToolStripSeparator());

        // --- System -------------------------------------------------------
        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked      = StartupService.IsEnabled(),
            CheckOnClick = true,
            Image        = RenderGlyph(FluentIcon.Play, colors.Foreground),
        };
        startupItem.CheckedChanged += (_, _) => StartupService.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(MakeItem("Exit", FluentIcon.Exit, colors, (_, _) => Shutdown()));

        // Live updates -----------------------------------------------------
        _voiceService.ActiveCulturesChanged += (_, _) => RefreshStatus();

        return new NotifyIcon
        {
            Icon             = CreateTrayIcon(),
            Text             = "Windows Assistant — Listening",
            ContextMenuStrip = menu,
            Visible          = true,
        };
    }

    private string BuildStatusText()
    {
        var cultures = _voiceService.ActiveCultures;
        if (cultures.Count == 0) return "Status: no languages active";

        var names = string.Join(" + ", cultures.Select(PrettyCultureName));
        return $"Status: listening ({names})";
    }

    private string BuildMonitorsText() =>
        _monitorService.Count == 0
            ? "Monitors: none detected"
            : $"Monitors: {_monitorService.Count} detected";

    private static string PrettyCultureName(string name) => name switch
    {
        "pt-BR" => "Português",
        "en-US" => "English",
        _       => name,
    };

    private void RefreshStatus()
    {
        if (_statusItem is not null)   _statusItem.Text   = BuildStatusText();
        if (_monitorsItem is not null) _monitorsItem.Text = BuildMonitorsText();
    }

    private ToolStripMenuItem BuildLanguagesMenu()
    {
        var menu = new ToolStripMenuItem("Languages");

        foreach (var culture in VoiceListenerService.KnownCultures)
        {
            var item = new ToolStripMenuItem($"{PrettyCultureName(culture)} ({culture})")
            {
                Tag          = culture,
                Checked      = _voiceService.IsCultureActive(culture),
                CheckOnClick = true,
            };
            item.Click += (s, _) => OnLanguageToggled((ToolStripMenuItem)s!);
            menu.DropDownItems.Add(item);
        }

        _voiceService.ActiveCulturesChanged += (_, _) =>
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
                item.Checked = _voiceService.IsCultureActive((string)item.Tag!);
        };

        return menu;
    }

    private void OnLanguageToggled(ToolStripMenuItem item)
    {
        var culture = (string)item.Tag!;
        _voiceService.SetCultureEnabled(culture, item.Checked);

        var state = item.Checked ? "enabled" : "disabled";
        _trayIcon.ShowBalloonTip(2000, "Windows Assistant",
            $"{PrettyCultureName(culture)} {state}", ToolTipIcon.Info);
    }

    private ToolStripMenuItem BuildSpeedMenu()
    {
        var speedMenu = new ToolStripMenuItem("Speech speed");

        foreach (SpeechSpeed speed in Enum.GetValues<SpeechSpeed>())
        {
            var item = new ToolStripMenuItem(speed.ToString())
            {
                Tag     = speed,
                Checked = speed == _voiceService.Speed,
            };
            item.Click += OnSpeedItemClicked;
            speedMenu.DropDownItems.Add(item);
        }

        _voiceService.SpeedChanged += (_, newSpeed) =>
        {
            foreach (ToolStripMenuItem item in speedMenu.DropDownItems)
                item.Checked = (SpeechSpeed)item.Tag! == newSpeed;
        };

        return speedMenu;
    }

    private void OnSpeedItemClicked(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item && item.Tag is SpeechSpeed speed)
        {
            _voiceService.SetSpeed(speed);
            _trayIcon.ShowBalloonTip(2000, "Windows Assistant", $"Speech speed: {speed}", ToolTipIcon.Info);
        }
    }

    private void ShowHelp()
    {
        var dialog = new HelpDialog();
        dialog.ShowDialog();
        dialog.Dispose();
    }

    private void OnShowMonitorInfo(object? sender, EventArgs e)
    {
        var descriptions = _monitorService.GetMonitorDescriptions();
        var body = descriptions.Count == 0
            ? "No DDC/CI monitors detected."
            : string.Join("\n", descriptions.Select((d, i) => $"Monitor {i + 1}: {d}"));

        _trayIcon.ShowBalloonTip(4000, $"Monitors ({descriptions.Count})", body, ToolTipIcon.Info);
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void OnCommandExecuted(object? sender, CommandEventArgs e)
    {
        var icon = e.Result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning;
        _trayIcon.ShowBalloonTip(3000, "Windows Assistant", e.Result.Message, icon);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => RefreshMonitors();

    private void RefreshMonitors()
    {
        _monitorService.RefreshMonitors();
        int count = _monitorService.Count;
        _trayIcon.Text = $"Windows Assistant — {count} monitor{(count != 1 ? "s" : "")} detected";
        RefreshStatus();
    }

    // -------------------------------------------------------------------------
    // Fluent styling helpers
    // -------------------------------------------------------------------------

    /// <summary>Segoe Fluent Icons glyphs used on tray menu items.</summary>
    private static class FluentIcon
    {
        public const string Help       = "\uE897";
        public const string Monitor    = "\uE7F4";
        public const string Refresh    = "\uE72C";
        public const string Globe      = "\uF2B7";
        public const string Microphone = "\uE720";
        public const string Play       = "\uE768";
        public const string Exit       = "\uE7E8";
    }

    private static Font PickMenuFont()
    {
        // Segoe UI Variable is the Windows 11 default; fall back gracefully
        // on older systems that only ship Segoe UI.
        foreach (var family in new[] { "Segoe UI Variable Display", "Segoe UI Variable", "Segoe UI" })
        {
            try
            {
                var f = new Font(family, 9.5f);
                if (f.Name == family) return f;
                f.Dispose();
            }
            catch { /* try next */ }
        }
        return SystemFonts.MenuFont ?? new Font("Tahoma", 9f);
    }

    private static ToolStripMenuItem MakeItem(
        string text, string glyph, FluentMenuRenderer.FluentColors colors, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            Image = RenderGlyph(glyph, colors.Foreground),
        };
        item.Click += onClick;
        return item;
    }

    /// <summary>Renders a Segoe Fluent Icons glyph to a 16x16 transparent bitmap.</summary>
    private static Image RenderGlyph(string glyph, Color color)
    {
        const int size = 16;
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        string fontName = FontExists("Segoe Fluent Icons")
            ? "Segoe Fluent Icons"
            : "Segoe MDL2 Assets"; // Win10 fallback

        using var font  = new Font(fontName, 10f, FontStyle.Regular, GraphicsUnit.Point);
        using var brush = new SolidBrush(color);
        var format = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
        return bmp;
    }

    private static bool FontExists(string name)
    {
        try
        {
            using var f = new Font(name, 10f);
            return f.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Asks DWM to render the menu popup window with Windows 11 rounded corners.</summary>
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
            // Pre-Windows 11 systems don't support this attribute — degrade silently
        }
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Shutdown()
    {
        _voiceService.Stop();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _trayIcon.Dispose();
            _voiceService.Dispose();
            _monitorService.Dispose();
        }
        _disposed = true;
        base.Dispose(disposing);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Draws a simple "WA" tray icon at 16×16 without needing an .ico file.</summary>
    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillEllipse(Brushes.DodgerBlue, 0, 0, 15, 15);
        g.DrawString("W", new Font("Arial", 6, FontStyle.Bold), Brushes.White, 1f, 2f);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
