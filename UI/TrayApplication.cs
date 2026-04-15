using Microsoft.Win32;
using WindowsAssistant.Commands;
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
    private bool _disposed;

    public TrayApplication()
    {
        // Check and offer to install missing speech language packs before anything else
        LanguageSetupService.CheckAndPromptInstall();

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
        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked      = StartupService.IsEnabled(),
            CheckOnClick = true,
        };
        startupItem.CheckedChanged += (_, _) => StartupService.SetEnabled(startupItem.Checked);

        var menu = new ContextMenuStrip();

        var header = (ToolStripMenuItem)menu.Items.Add("Windows Assistant");
        header.Enabled = false;

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Help / Tutorial", null, (_, _) => ShowHelp());
        menu.Items.Add("Monitors detected: …", null, OnShowMonitorInfo);
        menu.Items.Add("Refresh monitors",      null, (_, _) => RefreshMonitors());
        menu.Items.Add(BuildSpeedMenu());
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        return new NotifyIcon
        {
            Icon             = CreateTrayIcon(),
            Text             = "Windows Assistant — Listening",
            ContextMenuStrip = menu,
            Visible          = true,
        };
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
