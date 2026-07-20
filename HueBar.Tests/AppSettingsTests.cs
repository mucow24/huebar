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

    // ---- IsConnected ----------------------------------------------------------

    [Theory]
    [InlineData("10.0.0.5", "KEY", true)]
    [InlineData(null, "KEY", false)]
    [InlineData("10.0.0.5", null, false)]
    [InlineData("", "KEY", false)]
    [InlineData("10.0.0.5", "", false)]
    [InlineData("   ", "KEY", false)]
    [InlineData(null, null, false)]
    public void IsConnected_requires_both_ip_and_username(string? ip, string? user, bool expected)
    {
        var settings = new AppSettings { BridgeIp = ip, Username = user };

        Assert.Equal(expected, settings.IsConnected);
    }

    // ---- Save / Load round-trip ----------------------------------------------

    [Fact]
    public void Save_then_Load_round_trips_all_fields()
    {
        var original = new AppSettings { BridgeIp = "192.168.1.2", Username = "secretKey", IncludeZones = false };
        original.Save(_path);

        var loaded = AppSettings.Load(_path);

        Assert.Equal("192.168.1.2", loaded.BridgeIp);
        Assert.Equal("secretKey", loaded.Username);
        Assert.False(loaded.IncludeZones);
    }

    [Fact]
    public void Save_creates_the_settings_directory_if_missing()
    {
        Assert.False(Directory.Exists(_dir));

        new AppSettings { BridgeIp = "1.2.3.4", Username = "k" }.Save(_path);

        Assert.True(File.Exists(_path));
    }

    // ---- Load resilience ------------------------------------------------------

    [Fact]
    public void Load_returns_defaults_when_the_file_is_missing()
    {
        var settings = AppSettings.Load(_path);

        Assert.Null(settings.BridgeIp);
        Assert.Null(settings.Username);
        Assert.True(settings.IncludeZones); // default
        Assert.False(settings.IsConnected);
    }

    [Fact]
    public void Load_recovers_with_defaults_when_the_file_is_corrupt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not valid json ]");

        var settings = AppSettings.Load(_path);

        Assert.False(settings.IsConnected);
        Assert.Null(settings.BridgeIp);
    }

    [Fact]
    public void IncludeZones_defaults_to_true_for_settings_saved_before_the_field_existed()
    {
        // A settings.json written by an older build won't have the IncludeZones key.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{ "BridgeIp": "1.2.3.4", "Username": "k" }""");

        var settings = AppSettings.Load(_path);

        Assert.True(settings.IncludeZones);
        Assert.True(settings.IsConnected);
    }
}
