using System.Diagnostics;
using System.Globalization;
using System.Speech.Recognition;

namespace WindowsAssistant.Services;

/// <summary>
/// Checks whether the required speech recognition language packs are installed
/// and offers to install missing ones via Windows capabilities (requires admin).
/// </summary>
public static class LanguageSetupService
{
    private static readonly Dictionary<string, string> RequiredCultures = new()
    {
        ["en-US"] = "Language.Speech~~~en-US~0.0.1.0",
        ["pt-BR"] = "Language.Speech~~~pt-BR~0.0.1.0",
    };

    /// <summary>Returns culture names that have a speech recognizer installed.</summary>
    public static List<string> GetInstalledCultures()
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers()
            .Select(r => r.Culture.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RequiredCultures.Keys
            .Where(c => installed.Contains(c))
            .ToList();
    }

    /// <summary>Returns culture names that are required but not installed.</summary>
    public static List<string> GetMissingCultures()
    {
        var installed = GetInstalledCultures().ToHashSet();
        return RequiredCultures.Keys
            .Where(c => !installed.Contains(c))
            .ToList();
    }

    /// <summary>
    /// Prompts the user and installs missing speech language packs.
    /// Returns <c>true</c> if all languages are now available (or were already installed).
    /// </summary>
    public static bool CheckAndPromptInstall()
    {
        var missing = GetMissingCultures();
        if (missing.Count == 0)
            return true;

        var names = string.Join(", ", missing);
        var result = MessageBox.Show(
            $"The following speech recognition languages are not installed:\n\n" +
            $"  {names}\n\n" +
            "Without them, voice commands in those languages won't work.\n\n" +
            "Install now? (requires administrator privileges)",
            "Windows Assistant — Language Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return false;

        bool allSucceeded = true;
        foreach (var culture in missing)
        {
            if (RequiredCultures.TryGetValue(culture, out var capability))
            {
                if (!InstallCapability(capability))
                    allSucceeded = false;
            }
        }

        if (allSucceeded)
        {
            MessageBox.Show(
                "Language packs installed successfully.\n" +
                "A restart of the app may be required for changes to take effect.",
                "Windows Assistant",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(
                "Some language packs could not be installed.\n" +
                "You can install them manually in:\n" +
                "Settings → Time & Language → Language → Add a language",
                "Windows Assistant",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return allSucceeded;
    }

    /// <summary>
    /// Runs Add-WindowsCapability as admin via PowerShell.
    /// Returns <c>true</c> if the process exited with code 0.
    /// </summary>
    private static bool InstallCapability(string capabilityName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName  = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Add-WindowsCapability -Online -Name '{capabilityName}'\"",
                Verb      = "runas",
                UseShellExecute = true,
                WindowStyle     = WindowStyle.Hidden,
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            process.WaitForExit(TimeSpan.FromMinutes(5));
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            // User cancelled UAC or other error
            return false;
        }
    }
}
