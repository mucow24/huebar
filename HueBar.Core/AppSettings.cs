using System.Text.Json;
using System.Text.Json.Serialization;

namespace HueBar.Core;

/// <summary>
/// One paired Hue bridge: its network address plus the application key ("username") the bridge
/// issued during pairing. That key does not expire, so once a bridge is in the list HueBar can
/// switch to it without pressing the physical link button again.
/// </summary>
public sealed class BridgeEntry
{
    /// <summary>
    /// Stable identity. The bridge's own <c>bridgeid</c> (from <c>/config</c>) when known,
    /// otherwise the IP — stable enough on a home network. Used as the key for the active-bridge
    /// pointer and to de-duplicate a re-pair of a bridge already in the list.
    /// </summary>
    public string Id { get; set; } = "";

    public string BridgeIp { get; set; } = "";

    /// <summary>The application key the bridge issued during pairing.</summary>
    public string Username { get; set; } = "";

    /// <summary>Friendly name from the bridge's <c>/config</c>; may be null (then the IP is shown).</summary>
    public string? Name { get; set; }

    /// <summary>Has both the address and the key needed to talk to the bridge.</summary>
    [JsonIgnore]
    public bool IsUsable => !string.IsNullOrWhiteSpace(BridgeIp) && !string.IsNullOrWhiteSpace(Username);

    /// <summary>What the settings list shows for this bridge: its name, or the IP if it has none.</summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? BridgeIp : Name!;
}

/// <summary>
/// Persistent app configuration, stored at %AppData%\HueBar\settings.json. Holds the list of
/// paired bridges and which one is currently active, plus display options.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Every bridge HueBar has been paired with.</summary>
    public List<BridgeEntry> Bridges { get; set; } = new();

    /// <summary><see cref="BridgeEntry.Id"/> of the bridge the tray currently controls.</summary>
    public string? ActiveBridgeId { get; set; }

    public bool IncludeZones { get; set; } = true;

    // --- Legacy single-bridge fields --------------------------------------------------------
    // A pre-multi-bridge settings.json stored one bridge at the top level. These properties let
    // such a file still deserialize; Migrate() (run by Load) folds them into Bridges and then
    // nulls them so they are never written back. They are not read anywhere else.
    public string? BridgeIp { get; set; }
    public string? Username { get; set; }

    /// <summary>The active bridge, or null when none is selected / the pointer is dangling.</summary>
    [JsonIgnore]
    public BridgeEntry? ActiveBridge => Bridges.FirstOrDefault(b => b.Id == ActiveBridgeId);

    /// <summary>True when there is a usable active bridge to talk to.</summary>
    [JsonIgnore]
    public bool IsConnected => ActiveBridge is { IsUsable: true };

    /// <summary>
    /// Adds a bridge, or updates the matching existing one, and makes it active. Matches an
    /// existing entry by <paramref name="id"/> first, then by <paramref name="ip"/> — so
    /// re-pairing a bridge already known only by its IP (e.g. one migrated from an old settings
    /// file) adopts its real bridge id in place rather than creating a duplicate. A null/blank
    /// <paramref name="name"/> leaves any existing name untouched.
    /// </summary>
    public BridgeEntry AddOrUpdateBridge(string id, string ip, string username, string? name = null)
    {
        var entry = Bridges.FirstOrDefault(b => b.Id == id)
                    ?? Bridges.FirstOrDefault(b => b.BridgeIp == ip);
        if (entry is null)
        {
            entry = new BridgeEntry();
            Bridges.Add(entry);
        }

        entry.Id = id;
        entry.BridgeIp = ip;
        entry.Username = username;
        if (!string.IsNullOrWhiteSpace(name))
            entry.Name = name;

        ActiveBridgeId = id;
        return entry;
    }

    /// <summary>Switches the active bridge. No-op if no bridge has that id.</summary>
    public void SetActiveBridge(string id)
    {
        if (Bridges.Any(b => b.Id == id))
            ActiveBridgeId = id;
    }

    /// <summary>
    /// Forgets a bridge. If it was the active one, the active pointer moves to the first remaining
    /// bridge, or becomes null when none remain.
    /// </summary>
    public void RemoveBridge(string id)
    {
        Bridges.RemoveAll(b => b.Id == id);
        if (ActiveBridgeId == id)
            ActiveBridgeId = Bridges.FirstOrDefault()?.Id;
    }

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HueBar");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Keeps the migrated (now-null) legacy fields — and any null Name/ActiveBridgeId — out of
        // the file, so what we write is the clean multi-bridge shape.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppSettings Load() => Load(SettingsPath);

    /// <summary>
    /// Loads settings from an explicit path. A missing, empty, or corrupt file yields fresh
    /// defaults rather than throwing, so a bad file can never keep the app from starting. An old
    /// single-bridge file is migrated to the bridge list on the way out.
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
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Migrate();
            return settings;
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
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Folds a pre-multi-bridge file (single BridgeIp/Username at the top level) into the Bridges
    /// list, then clears the legacy fields so they are not written back. A no-op for files that
    /// already use the bridge list.
    /// </summary>
    private void Migrate()
    {
        if (Bridges.Count == 0 && !string.IsNullOrWhiteSpace(BridgeIp) && !string.IsNullOrWhiteSpace(Username))
        {
            var id = BridgeIp!; // legacy files carry no bridgeid; the IP is the stable-enough key
            Bridges.Add(new BridgeEntry { Id = id, BridgeIp = BridgeIp!, Username = Username! });
            ActiveBridgeId = id;
        }

        BridgeIp = null;
        Username = null;
    }
}
