using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsAssistant.Services;

/// <summary>
/// User-configurable preferences persisted as JSON in
/// <c>%LOCALAPPDATA%\WindowsAssistant\settings.json</c>. Everything is
/// mutable so the tray menu can update a single field and call <see cref="Save"/>
/// without re-serializing by hand.
/// </summary>
public sealed class AppSettings
{
    private const string DefaultWakePhrase   = "computador";
    private const string DefaultCulture      = "pt-BR";

    [JsonPropertyName("wakePhrase")]
    public string WakePhrase { get; set; } = DefaultWakePhrase;

    [JsonPropertyName("activeCulture")]
    public string ActiveCulture { get; set; } = DefaultCulture;

    // -------------------------------------------------------------------------
    // Persistence
    // -------------------------------------------------------------------------

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsAssistant",
        "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Loads settings from disk. Any failure returns defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            return Normalize(loaded ?? new AppSettings());
        }
        catch
        {
            // Corrupted or partial settings shouldn't crash the app
            return new AppSettings();
        }
    }

    /// <summary>Persists the current values to disk. Swallows IO errors.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(this, SerializerOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Disk full, readonly profile, etc. — not worth aborting for
        }
    }

    private static AppSettings Normalize(AppSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.WakePhrase))    s.WakePhrase    = DefaultWakePhrase;
        if (string.IsNullOrWhiteSpace(s.ActiveCulture)) s.ActiveCulture = DefaultCulture;
        s.WakePhrase = s.WakePhrase.Trim().ToLowerInvariant();
        return s;
    }
}
