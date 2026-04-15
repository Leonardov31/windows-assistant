using Microsoft.Win32;

namespace WindowsAssistant.Services;

/// <summary>
/// Manages the Windows run-on-startup registry entry for this application.
/// </summary>
public static class StartupService
{
    private const string AppName      = "WindowsAssistant";
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue(AppName) is not null;
    }

    public static void SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
        if (key is null) return;

        if (enable)
            key.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
