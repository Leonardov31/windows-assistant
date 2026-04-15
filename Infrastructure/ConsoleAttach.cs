namespace WindowsAssistant.Infrastructure;

/// <summary>
/// Attaches the WinExe process to its parent terminal's console (if any) so
/// <see cref="Console.WriteLine"/> output streams back to the shell that
/// launched the app. Safe to call multiple times; failures are swallowed.
///
/// Rationale: the app is configured as <c>WinExe</c> (no console window by
/// default), but every voice transcription is written to stdout so the user
/// can tail recognition live when running <c>WindowsAssistant.exe</c> from
/// a terminal. Tray launches (no parent console) simply get a no-op.
/// </summary>
internal static class ConsoleAttach
{
    private static bool _attached;

    /// <summary>
    /// Tries to attach to the parent process's console. In DEBUG builds,
    /// if no parent console exists, allocates a new console window as a
    /// fallback so the app is still debuggable without a parent shell.
    /// </summary>
    public static void EnsureAttached()
    {
        if (_attached) return;
        _attached = true;

        try
        {
            if (NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
                return;
        }
        catch
        {
            // Fall through — AttachConsole may throw on some loader states
        }

#if DEBUG
        try { NativeMethods.AllocConsole(); }
        catch { /* last-resort; safe to ignore */ }
#endif
    }
}
