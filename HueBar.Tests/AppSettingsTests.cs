using System.Text.Json;
using HueBar.Core;

namespace HueBar.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public AppSettingsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "huebar-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static AppSettings WithBridges(params BridgeEntry[] bridges)
    {
        var settings = new AppSettings();
        settings.Bridges.AddRange(bridges);
        settings.ActiveBridgeId = bridges.FirstOrDefault()?.Id;
        return settings;
    }

    private static BridgeEntry Bridge(string id, string ip = "10.0.0.5", string user = "KEY", string? name = null)
        => new() { Id = id, BridgeIp = ip, Username = user, Name = name };

    // ---- BridgeEntry ----------------------------------------------------------

    [Theory]
    [InlineData("10.0.0.5", "KEY", true)]
    [InlineData(null, "KEY", false)]
    [InlineData("10.0.0.5", null, false)]
    [InlineData("", "KEY", false)]
    [InlineData("10.0.0.5", "", false)]
    [InlineData("   ", "KEY", false)]
    public void BridgeEntry_IsUsable_requires_both_ip_and_key(string? ip, string? key, bool expected)
    {
        var entry = new BridgeEntry { BridgeIp = ip ?? "", Username = key ?? "" };

        Assert.Equal(expected, entry.IsUsable);
    }

    [Fact]
    public void DisplayName_is_the_name_when_set_and_the_ip_otherwise()
    {
        Assert.Equal("Living room", new BridgeEntry { BridgeIp = "1.2.3.4", Name = "Living room" }.DisplayName);
        Assert.Equal("1.2.3.4", new BridgeEntry { BridgeIp = "1.2.3.4", Name = null }.DisplayName);
        Assert.Equal("1.2.3.4", new BridgeEntry { BridgeIp = "1.2.3.4", Name = "  " }.DisplayName);
    }

    // ---- IsConnected / ActiveBridge ------------------------------------------

    [Fact]
    public void IsConnected_is_false_with_no_bridges()
    {
        var settings = new AppSettings();

        Assert.Null(settings.ActiveBridge);
        Assert.False(settings.IsConnected);
    }

    [Fact]
    public void IsConnected_tracks_the_active_bridge()
    {
        var settings = WithBridges(Bridge("A"), Bridge("B"));

        Assert.Equal("A", settings.ActiveBridge!.Id);
        Assert.True(settings.IsConnected);
    }

    [Fact]
    public void IsConnected_is_false_when_the_active_pointer_dangles()
    {
        var settings = WithBridges(Bridge("A"));
        settings.ActiveBridgeId = "does-not-exist";

        Assert.Null(settings.ActiveBridge);
        Assert.False(settings.IsConnected);
    }

    [Fact]
    public void IsConnected_is_false_when_the_active_bridge_lacks_a_key()
    {
        var settings = WithBridges(Bridge("A", user: ""));

        Assert.False(settings.IsConnected);
    }

    // ---- AddOrUpdateBridge ----------------------------------------------------

    [Fact]
    public void AddOrUpdateBridge_adds_a_new_bridge_and_makes_it_active()
    {
        var settings = new AppSettings();

        var entry = settings.AddOrUpdateBridge("id-1", "1.2.3.4", "key1", "Studio");

        Assert.Single(settings.Bridges);
        Assert.Equal("id-1", settings.ActiveBridgeId);
        Assert.Same(entry, settings.ActiveBridge);
        Assert.Equal("Studio", entry.Name);
        Assert.True(settings.IsConnected);
    }

    [Fact]
    public void AddOrUpdateBridge_updates_the_same_id_in_place_rather_than_duplicating()
    {
        var settings = new AppSettings();
        settings.AddOrUpdateBridge("id-1", "1.2.3.4", "key1", "Studio");

        settings.AddOrUpdateBridge("id-1", "1.2.3.9", "key2"); // same bridge, new IP + fresh key

        var entry = Assert.Single(settings.Bridges);
        Assert.Equal("1.2.3.9", entry.BridgeIp);
        Assert.Equal("key2", entry.Username);
        Assert.Equal("Studio", entry.Name); // null name must not clobber the existing one
    }

    [Fact]
    public void AddOrUpdateBridge_matches_by_ip_and_adopts_the_real_id_for_a_repaired_bridge()
    {
        // Mirrors a migrated legacy bridge (id == its IP) being re-paired once its bridgeid is known.
        var settings = WithBridges(Bridge("1.2.3.4", ip: "1.2.3.4", user: "old"));

        settings.AddOrUpdateBridge("001788fffe123456", "1.2.3.4", "new", "Living room");

        var entry = Assert.Single(settings.Bridges); // no duplicate
        Assert.Equal("001788fffe123456", entry.Id);
        Assert.Equal("new", entry.Username);
        Assert.Equal("Living room", entry.Name);
        Assert.Equal("001788fffe123456", settings.ActiveBridgeId);
    }

    [Fact]
    public void AddOrUpdateBridge_overwrites_a_name_only_when_a_new_one_is_given()
    {
        var settings = new AppSettings();
        settings.AddOrUpdateBridge("id-1", "1.2.3.4", "k", "First");

        settings.AddOrUpdateBridge("id-1", "1.2.3.4", "k", "Second");

        Assert.Equal("Second", settings.Bridges.Single().Name);
    }

    // ---- SetActiveBridge ------------------------------------------------------

    [Fact]
    public void SetActiveBridge_switches_between_known_bridges()
    {
        var settings = WithBridges(Bridge("A"), Bridge("B"));

        settings.SetActiveBridge("B");

        Assert.Equal("B", settings.ActiveBridgeId);
        Assert.Equal("B", settings.ActiveBridge!.Id);
    }

    [Fact]
    public void SetActiveBridge_ignores_an_unknown_id()
    {
        var settings = WithBridges(Bridge("A"), Bridge("B"));

        settings.SetActiveBridge("nope");

        Assert.Equal("A", settings.ActiveBridgeId); // unchanged
    }

    // ---- RemoveBridge ---------------------------------------------------------

    [Fact]
    public void RemoveBridge_of_the_active_one_repoints_to_the_first_remaining()
    {
        var settings = WithBridges(Bridge("A"), Bridge("B"), Bridge("C"));
        settings.SetActiveBridge("A");

        settings.RemoveBridge("A");

        Assert.DoesNotContain(settings.Bridges, b => b.Id == "A");
        Assert.Equal("B", settings.ActiveBridgeId);
        Assert.True(settings.IsConnected);
    }

    [Fact]
    public void RemoveBridge_of_the_last_bridge_leaves_nothing_active()
    {
        var settings = WithBridges(Bridge("A"));

        settings.RemoveBridge("A");

        Assert.Empty(settings.Bridges);
        Assert.Null(settings.ActiveBridgeId);
        Assert.False(settings.IsConnected);
    }

    [Fact]
    public void RemoveBridge_of_an_inactive_one_leaves_the_active_pointer_alone()
    {
        var settings = WithBridges(Bridge("A"), Bridge("B"));
        settings.SetActiveBridge("A");

        settings.RemoveBridge("B");

        Assert.Equal("A", settings.ActiveBridgeId);
    }

    // ---- Save / Load round-trip ----------------------------------------------

    [Fact]
    public void Save_then_Load_round_trips_the_bridge_list_and_active_pointer()
    {
        var original = new AppSettings { IncludeZones = false };
        original.AddOrUpdateBridge("id-A", "192.168.1.2", "keyA", "Living room");
        original.AddOrUpdateBridge("id-B", "192.168.1.3", "keyB", "Studio");
        original.SetActiveBridge("id-A");
        original.Save(_path);

        var loaded = AppSettings.Load(_path);

        Assert.Equal(2, loaded.Bridges.Count);
        Assert.Equal("id-A", loaded.ActiveBridgeId);
        Assert.Equal("Living room", loaded.ActiveBridge!.Name);
        Assert.Equal("keyB", loaded.Bridges.Single(b => b.Id == "id-B").Username);
        Assert.False(loaded.IncludeZones);
        Assert.True(loaded.IsConnected);
    }

    [Fact]
    public void Save_creates_the_settings_directory_if_missing()
    {
        Assert.False(Directory.Exists(_dir));

        var settings = new AppSettings();
        settings.AddOrUpdateBridge("id-1", "1.2.3.4", "k");
        settings.Save(_path);

        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Saved_file_does_not_carry_the_legacy_top_level_single_bridge_fields()
    {
        var settings = new AppSettings();
        settings.AddOrUpdateBridge("id-1", "1.2.3.4", "k");
        settings.Save(_path);

        // The legacy fields share names with the per-bridge ones, so check the *root* object only:
        // BridgeIp/Username must appear nested under Bridges, never at the top level.
        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("BridgeIp", out _));
        Assert.False(root.TryGetProperty("Username", out _));
        Assert.True(root.GetProperty("Bridges")[0].TryGetProperty("BridgeIp", out _));
    }

    // ---- Load resilience ------------------------------------------------------

    [Fact]
    public void Load_returns_defaults_when_the_file_is_missing()
    {
        var settings = AppSettings.Load(_path);

        Assert.Empty(settings.Bridges);
        Assert.Null(settings.ActiveBridgeId);
        Assert.True(settings.IncludeZones); // default
        Assert.False(settings.IsConnected);
    }

    [Fact]
    public void Load_recovers_with_defaults_when_the_file_is_corrupt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not valid json ]");

        var settings = AppSettings.Load(_path);

        Assert.Empty(settings.Bridges);
        Assert.False(settings.IsConnected);
    }

    // ---- Legacy migration -----------------------------------------------------

    [Fact]
    public void Load_migrates_a_pre_multi_bridge_file_into_the_bridge_list()
    {
        // A settings.json written by the single-bridge build.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{ "BridgeIp": "1.2.3.4", "Username": "legacyKey", "IncludeZones": false }""");

        var settings = AppSettings.Load(_path);

        var entry = Assert.Single(settings.Bridges);
        Assert.Equal("1.2.3.4", entry.BridgeIp);
        Assert.Equal("legacyKey", entry.Username);
        Assert.Equal("1.2.3.4", entry.Id); // no bridgeid in a legacy file; the IP stands in
        Assert.Equal("1.2.3.4", settings.ActiveBridgeId);
        Assert.True(settings.IsConnected);
        Assert.False(settings.IncludeZones); // other fields still honored
    }

    [Fact]
    public void Migrated_settings_persist_in_the_new_shape_without_re_migrating()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{ "BridgeIp": "1.2.3.4", "Username": "legacyKey" }""");

        // Load (migrates), save (new shape), load again — the second load must not double up.
        var migrated = AppSettings.Load(_path);
        migrated.Save(_path);
        var reloaded = AppSettings.Load(_path);

        Assert.Single(reloaded.Bridges);
        Assert.Equal("1.2.3.4", reloaded.ActiveBridgeId);
        Assert.True(reloaded.IsConnected);
    }

    [Fact]
    public void IncludeZones_defaults_to_true_for_settings_saved_before_the_field_existed()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{ "BridgeIp": "1.2.3.4", "Username": "k" }""");

        var settings = AppSettings.Load(_path);

        Assert.True(settings.IncludeZones);
    }
}
