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

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // A corrupt/unreadable settings file should never crash the app; start fresh.
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
