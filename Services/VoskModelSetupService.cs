using System.IO.Compression;
using System.Net.Http;

namespace WindowsAssistant.Services;

/// <summary>
/// Ensures the required Vosk speech recognition models are present on disk,
/// downloading them from alphacephei.com on first run.
///
/// Models are stored in %LOCALAPPDATA%\WindowsAssistant\Models\{culture}\.
/// No admin / UAC elevation is required.
/// </summary>
public static class VoskModelSetupService
{
    private sealed record ModelInfo(string Culture, string FolderName, string ZipUrl);

    private static readonly ModelInfo[] RequiredModels =
    [
        new("pt-BR", "vosk-model-small-pt-0.3",    "https://alphacephei.com/vosk/models/vosk-model-small-pt-0.3.zip"),
        new("en-US", "vosk-model-small-en-us-0.15","https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"),
    ];

    /// <summary>Root directory that holds one subfolder per culture.</summary>
    public static string ModelsRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsAssistant",
        "Models");

    /// <summary>Returns the absolute path of the model for a given culture, or null if not installed.</summary>
    public static string? GetModelPath(string cultureName)
    {
        var info = RequiredModels.FirstOrDefault(m => m.Culture == cultureName);
        if (info is null) return null;

        var path = Path.Combine(ModelsRoot, info.Culture, info.FolderName);
        return IsValidVoskModel(path) ? path : null;
    }

    /// <summary>
    /// Downloads and extracts any missing Vosk models. Shows progress via MessageBox
    /// (blocking). Returns true when all required models are present.
    /// </summary>
    public static bool EnsureModelsAvailable()
    {
        Directory.CreateDirectory(ModelsRoot);

        var missing = RequiredModels
            .Where(m => !IsValidVoskModel(Path.Combine(ModelsRoot, m.Culture, m.FolderName)))
            .ToList();

        if (missing.Count == 0)
            return true;

        var names = string.Join(", ", missing.Select(m => m.Culture));
        var choice = MessageBox.Show(
            $"Voice recognition models are not installed yet:\n\n  {names}\n\n" +
            $"They will be downloaded from alphacephei.com (~40 MB each) to:\n{ModelsRoot}\n\n" +
            "Download now?",
            "Windows Assistant — Speech Models",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (choice != DialogResult.Yes)
            return false;

        bool allSucceeded = true;
        foreach (var model in missing)
        {
            if (!TryDownloadAndExtract(model))
                allSucceeded = false;
        }

        if (allSucceeded)
        {
            MessageBox.Show(
                "Speech models installed successfully.",
                "Windows Assistant",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(
                "Some models could not be downloaded. Check your internet connection\n" +
                "and restart the app to try again.",
                "Windows Assistant",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return allSucceeded;
    }

    /// <summary>
    /// A Vosk model directory must contain the 'am', 'graph' and 'conf' subfolders.
    /// </summary>
    private static bool IsValidVoskModel(string modelPath)
    {
        if (!Directory.Exists(modelPath)) return false;
        return Directory.Exists(Path.Combine(modelPath, "am"))
            && Directory.Exists(Path.Combine(modelPath, "graph"))
            && Directory.Exists(Path.Combine(modelPath, "conf"));
    }

    private static bool TryDownloadAndExtract(ModelInfo model)
    {
        var cultureDir = Path.Combine(ModelsRoot, model.Culture);
        Directory.CreateDirectory(cultureDir);

        var zipPath = Path.Combine(cultureDir, model.FolderName + ".zip");

        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(10);

            using (var response = http.GetAsync(model.ZipUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var dest = File.Create(zipPath);
                source.CopyTo(dest);
            }

            ZipFile.ExtractToDirectory(zipPath, cultureDir, overwriteFiles: true);
            File.Delete(zipPath);

            return IsValidVoskModel(Path.Combine(cultureDir, model.FolderName));
        }
        catch (Exception ex)
        {
            LogError($"Failed to download Vosk model for {model.Culture}: {ex.Message}");
            return false;
        }
    }

    private static void LogError(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsAssistant");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "voice.log"),
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Don't let logging failures break setup
        }
    }
}
