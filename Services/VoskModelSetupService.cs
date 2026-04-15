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

        var failures = new List<string>();
        foreach (var model in missing)
        {
            var error = TryDownloadAndExtract(model);
            if (error is not null)
                failures.Add($"{model.Culture}: {error}");
        }

        if (failures.Count == 0)
        {
            MessageBox.Show(
                "Speech models installed successfully.",
                "Windows Assistant",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }

        MessageBox.Show(
            "Some models could not be downloaded:\n\n" +
            string.Join("\n", failures) + "\n\n" +
            $"See: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsAssistant", "voice.log")}\n" +
            "Restart the app to try again.",
            "Windows Assistant",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>
    /// A Vosk model directory must contain the <c>ivector</c> subfolder
    /// (present in every officially released model) plus the acoustic model
    /// file. Newer models nest it under <c>am/final.mdl</c>; older small
    /// models (e.g. vosk-model-small-pt-0.3) place <c>final.mdl</c> at the
    /// root alongside <c>HCLr.fst</c> / <c>Gr.fst</c>.
    /// </summary>
    private static bool IsValidVoskModel(string modelPath)
    {
        if (!Directory.Exists(modelPath)) return false;
        if (!Directory.Exists(Path.Combine(modelPath, "ivector"))) return false;

        bool hasNestedAM = File.Exists(Path.Combine(modelPath, "am", "final.mdl"));
        bool hasFlatAM   = File.Exists(Path.Combine(modelPath, "final.mdl"));
        return hasNestedAM || hasFlatAM;
    }

    /// <summary>
    /// Downloads the model ZIP and extracts it. Runs the entire async I/O on a
    /// worker thread to avoid the classic WinForms sync-over-async deadlock
    /// (HttpClient continuations trying to marshal back to a blocked UI thread).
    /// Returns <c>null</c> on success, or a short error message on failure.
    /// </summary>
    private static string? TryDownloadAndExtract(ModelInfo model)
    {
        var cultureDir = Path.Combine(ModelsRoot, model.Culture);
        Directory.CreateDirectory(cultureDir);

        var zipPath = Path.Combine(cultureDir, model.FolderName + ".zip");

        try
        {
            Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsAssistant/1.0");

                using var response = await http.GetAsync(model.ZipUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var dest   = File.Create(zipPath);
                await source.CopyToAsync(dest).ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            ZipFile.ExtractToDirectory(zipPath, cultureDir, overwriteFiles: true);
            File.Delete(zipPath);

            if (!IsValidVoskModel(Path.Combine(cultureDir, model.FolderName)))
                return "extracted archive is missing required subfolders";

            return null;
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            LogError($"Failed to install Vosk model for {model.Culture}: {ex.InnerException.Message}");
            return ex.InnerException.Message;
        }
        catch (Exception ex)
        {
            LogError($"Failed to install Vosk model for {model.Culture}: {ex.Message}");
            return ex.Message;
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
