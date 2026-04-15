using WindowsAssistant.Infrastructure;
using WindowsAssistant.UI;

// Stream diagnostic log lines to the parent terminal if launched from one.
// No-op when launched from the tray shortcut (no parent console to attach to).
ConsoleAttach.EnsureAttached();

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

// Opt into the Windows 11 dark/light theme for all WinForms controls.
// Requires .NET 9+ WinForms runtime.
Application.SetColorMode(SystemColorMode.System);

try
{
    using var app = new TrayApplication();
    Application.Run(app);
}
catch (Exception ex)
{
    var logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsAssistant", "crash.log");
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
    File.WriteAllText(logPath, $"{DateTime.Now:O}\n{ex}");

    Console.Error.WriteLine(ex);
    MessageBox.Show(ex.Message, "Windows Assistant — Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
