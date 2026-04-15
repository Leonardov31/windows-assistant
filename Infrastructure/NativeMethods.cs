using System.Runtime.InteropServices;

namespace WindowsAssistant.Infrastructure;

/// <summary>
/// P/Invoke declarations for DDC/CI monitor control via dxva2.dll and user32.dll.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PHYSICAL_MONITOR
    {
        internal IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left, Top, Right, Bottom;
    }

    internal delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor,
        out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor,
        uint dwPhysicalMonitorArraySize,
        [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetMonitorBrightness(
        IntPtr hPhysicalMonitor,
        out uint pdwMinimumBrightness,
        out uint pdwCurrentBrightness,
        out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool SetMonitorBrightness(
        IntPtr hPhysicalMonitor,
        uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool DestroyPhysicalMonitor(IntPtr hPhysicalMonitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool SetVCPFeature(
        IntPtr hPhysicalMonitor,
        byte bVCPCode,
        uint dwNewValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool GetVCPFeature(
        IntPtr hPhysicalMonitor,
        byte bVCPCode,
        out uint pvct,
        out uint pdwCurrentValue,
        out uint pdwMaximumValue);

    // VCP code 0xD6 — DPMS power state
    internal const byte VcpDisplayPower = 0xD6;
    internal const uint DpmsOn          = 1;   // D0: fully on
    internal const uint DpmsStandby     = 4;   // D1: standby

    // -------------------------------------------------------------------------
    // DWM — Windows 11 rounded corners on popup windows
    // -------------------------------------------------------------------------

    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE   = 20;

    internal enum DwmWindowCornerPreference
    {
        Default    = 0,
        DoNotRound = 1,
        Round      = 2,
        RoundSmall = 3,
    }

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int valueRef,
        int sizeOfValueRef);

    // -------------------------------------------------------------------------
    // kernel32 — attach the WinExe to the parent terminal's console so
    // diagnostic log lines stream to wherever the exe was launched from.
    // -------------------------------------------------------------------------

    internal const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllocConsole();
}
