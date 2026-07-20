using System.Text.Json;
using HueBar.Core;

namespace HueBar.Tests;

/// <summary>
/// Pins the <c>[JsonPropertyName]</c> wiring against the real (lower-cased, oddly-named)
/// shapes the Hue v1 bridge actually returns. If someone renames a property or drops an
/// attribute, these fail rather than silently deserializing to nulls at runtime.
/// </summary>
public class ModelSerializationTests
{
    [Fact]
    public void DiscoveredBridge_maps_internalipaddress()
    {
        var bridges = JsonSerializer.Deserialize<List<DiscoveredBridge>>(
            """[ { "id": "abc", "internalipaddress": "192.168.0.14", "port": 443 } ]""")!;

        Assert.Equal("192.168.0.14", bridges[0].InternalIpAddress);
        Assert.Equal("abc", bridges[0].Id);
        Assert.Equal(443, bridges[0].Port);
    }

    [Fact]
    public void HueGroup_maps_name_type_class_and_lights()
    {
        var group = JsonSerializer.Deserialize<HueGroup>(
            """{ "name": "Lounge", "type": "Room", "class": "Lounge", "lights": ["9","1","2"] }""")!;

        Assert.Equal("Lounge", group.Name);
        Assert.Equal("Room", group.Type);
        Assert.Equal("Lounge", group.Class);
        Assert.Equal(new[] { "9", "1", "2" }, group.Lights);
    }

    [Fact]
    public void HueScene_maps_name_type_group_and_lights()
    {
        var scene = JsonSerializer.Deserialize<HueScene>(
            """{ "name": "Nightlight", "type": "GroupScene", "group": "1", "lights": ["2"] }""")!;

        Assert.Equal("Nightlight", scene.Name);
        Assert.Equal("GroupScene", scene.Type);
        Assert.Equal("1", scene.Group);
        Assert.Equal(new[] { "2" }, scene.Lights);
    }

    [Fact]
    public void HueApiResponse_maps_the_link_button_error()
    {
        var responses = JsonSerializer.Deserialize<List<HueApiResponse>>(
            """[ { "error": { "type": 101, "address": "/", "description": "link button not pressed" } } ]""")!;

        Assert.Null(responses[0].Success);
        Assert.NotNull(responses[0].Error);
        Assert.Equal(101, responses[0].Error!.Type);
        Assert.Equal("link button not pressed", responses[0].Error!.Description);
    }

    [Fact]
    public void HueApiResponse_maps_the_success_element()
    {
        var responses = JsonSerializer.Deserialize<List<HueApiResponse>>(
            """[ { "success": { "username": "newkey" } } ]""")!;

        Assert.Null(responses[0].Error);
        Assert.NotNull(responses[0].Success);
        Assert.Equal("newkey", responses[0].Success!["username"].GetString());
    }

    /// <summary>
    /// End-to-end guard: bridge JSON → models → mapper, so the property attributes and the
    /// dictionary keying are proven to line up with what <see cref="RoomSceneMapper"/> reads.
    /// </summary>
    [Fact]
    public void Real_bridge_json_flows_through_models_into_the_mapper()
    {
        const string groupsJson = """
        {
          "1": { "name": "Lounge", "lights": ["9","1","2"], "type": "Room", "class": "Lounge" },
          "3": { "name": "Downstairs", "lights": ["9","1"], "type": "Zone", "class": "Other" },
          "7": { "name": "Ad hoc", "lights": ["5"], "type": "LightGroup" }
        }
        """;
        const string scenesJson = """
        {
          "8AuCtLbIiEJJRNB": { "name": "Nightlight", "type": "GroupScene", "group": "1" },
          "abc": { "name": "Energize", "type": "GroupScene", "group": "1" },
          "def": { "name": "Zone Chill", "type": "GroupScene", "group": "3" },
          "ghi": { "name": "Legacy", "type": "LightScene", "lights": ["1"] }
        }
        """;

        var groups = JsonSerializer.Deserialize<Dictionary<string, HueGroup>>(groupsJson)!;
        var scenes = JsonSerializer.Deserialize<Dictionary<string, HueScene>>(scenesJson)!;

        var rooms = RoomSceneMapper.BuildRooms(groups, scenes, includeZones: true);

        var lounge = rooms.Single(r => r.Name == "Lounge");
        Assert.Equal(new[] { "Energize", "Nightlight" }, lounge.Scenes.Select(s => s.Name));
        Assert.Equal(new[] { "Zone Chill" }, rooms.Single(r => r.Name == "Downstairs").Scenes.Select(s => s.Name));
        Assert.DoesNotContain(rooms, r => r.Name == "Ad hoc");
    }
}
