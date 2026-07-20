using System.Text.Json;

namespace HueBar.Core;

/// <summary>
/// Persistent app configuration, stored at %AppData%\HueBar\settings.json.
/// Holds the bridge IP and the application key ("username") obtained during pairing.
/// </summary>
public sealed class AppSettings
{
    public string? BridgeIp { get; set; }
    public string? Username { get; set; }
    public bool IncludeZones { get; set; } = true;

    public bool IsConnected => !string.IsNullOrWhiteSpace(BridgeIp) && !string.IsNullOrWhiteSpace(Username);

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HueBar");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load() => Load(SettingsPath);

    /// <summary>
    /// Loads settings from an explicit path. A missing, empty, or corrupt file yields fresh
    /// defaults rather than throwing, so a bad file can never keep the app from starting.
    /// (The path overload exists so the load/recover logic can be unit-tested without touching
    /// the real %AppData%.)
    /// </summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // A corrupt/unreadable settings file should never crash the app; start fresh.
            return new AppSettings();
        }
    }

    public void Save() => Save(SettingsPath);

    /// <summary>Serializes settings to an explicit path, creating its directory if needed.</summary>
    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
