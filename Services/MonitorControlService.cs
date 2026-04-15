using WindowsAssistant.Infrastructure;

namespace WindowsAssistant.Services;

/// <summary>
/// Enumerates physical monitors and exposes DDC/CI brightness control.
/// Call <see cref="RefreshMonitors"/> on startup and after display changes.
/// </summary>
public sealed class MonitorControlService : IDisposable
{
    private readonly List<PhysicalMonitor> _monitors = new();
    private bool _disposed;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public int Count => _monitors.Count;

    /// <summary>
    /// Re-enumerates all physical monitors connected to the system.
    /// Safe to call multiple times (releases previous handles first).
    /// </summary>
    public void RefreshMonitors()
    {
        ReleaseMonitorHandles();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero,
            EnumMonitorCallback,
            IntPtr.Zero);
    }

    /// <summary>Sets the brightness of a monitor (0–100).</summary>
    /// <param name="monitorIndex">Zero-based monitor index.</param>
    /// <param name="brightness">Target brightness percentage (0–100).</param>
    /// <returns><c>true</c> on success.</returns>
    public bool SetBrightness(int monitorIndex, uint brightness)
    {
        if (!IsValidIndex(monitorIndex)) return false;
        brightness = Math.Clamp(brightness, 0, 100);
        return NativeMethods.SetMonitorBrightness(_monitors[monitorIndex].Handle, brightness);
    }

    /// <summary>Gets the current brightness of a monitor.</summary>
    /// <param name="monitorIndex">Zero-based monitor index.</param>
    /// <returns>Current brightness (0–100), or <c>null</c> if unavailable.</returns>
    public uint? GetBrightness(int monitorIndex)
    {
        if (!IsValidIndex(monitorIndex)) return null;

        return NativeMethods.GetMonitorBrightness(
            _monitors[monitorIndex].Handle,
            out _, out uint current, out _)
            ? current
            : null;
    }

    /// <summary>Sets the brightness for all enumerated monitors.</summary>
    /// <returns><c>true</c> only if all monitors succeeded.</returns>
    public bool SetAllBrightness(uint brightness)
    {
        if (_monitors.Count == 0) return false;
        brightness = Math.Clamp(brightness, 0, 100);
        bool allOk = true;
        for (int i = 0; i < _monitors.Count; i++)
            allOk &= SetBrightness(i, brightness);
        return allOk;
    }

    /// <summary>Sets the DPMS power state of a monitor via VCP code 0xD6.</summary>
    /// <param name="monitorIndex">Zero-based monitor index.</param>
    /// <param name="on"><c>true</c> = power on, <c>false</c> = standby.</param>
    /// <returns><c>true</c> on success.</returns>
    public bool SetMonitorPower(int monitorIndex, bool on)
    {
        if (!IsValidIndex(monitorIndex)) return false;
        uint value = on ? NativeMethods.DpmsOn : NativeMethods.DpmsStandby;
        return NativeMethods.SetVCPFeature(
            _monitors[monitorIndex].Handle,
            NativeMethods.VcpDisplayPower,
            value);
    }

    /// <summary>Sets the DPMS power state for all enumerated monitors.</summary>
    /// <returns><c>true</c> only if all monitors succeeded.</returns>
    public bool SetAllMonitorsPower(bool on)
    {
        if (_monitors.Count == 0) return false;
        bool allOk = true;
        for (int i = 0; i < _monitors.Count; i++)
            allOk &= SetMonitorPower(i, on);
        return allOk;
    }

    /// <summary>Returns display-name descriptions for all detected monitors.</summary>
    public IReadOnlyList<string> GetMonitorDescriptions()
        => _monitors.Select(m => m.Description).ToList();

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private bool EnumMonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
    {
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
            return true;

        var physical = new NativeMethods.PHYSICAL_MONITOR[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
            return true;

        foreach (var m in physical)
            _monitors.Add(new PhysicalMonitor(m.hPhysicalMonitor, m.szPhysicalMonitorDescription));

        return true;
    }

    private bool IsValidIndex(int index) => index >= 0 && index < _monitors.Count;

    private void ReleaseMonitorHandles()
    {
        foreach (var m in _monitors)
            NativeMethods.DestroyPhysicalMonitor(m.Handle);
        _monitors.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        ReleaseMonitorHandles();
        _disposed = true;
    }

    private sealed record PhysicalMonitor(IntPtr Handle, string Description);
}
