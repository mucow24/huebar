using HueBar.Core;

namespace HueBar.Tests;

public class RoomSceneMapperTests
{
    // A representative bridge: two Rooms, one Zone, one LightGroup, and scenes of both
    // GroupScene and LightScene kinds.
    private static Dictionary<string, HueGroup> SampleGroups() => new()
    {
        ["1"] = new() { Name = "Living Room", Type = "Room", Lights = { "1", "2" } },
        ["2"] = new() { Name = "Kitchen", Type = "Room", Lights = { "3" } },
        ["3"] = new() { Name = "Downstairs", Type = "Zone", Lights = { "1", "3" } },
        ["9"] = new() { Name = "TV Backlight", Type = "LightGroup", Lights = { "4" } },
    };

    private static Dictionary<string, HueScene> SampleScenes() => new()
    {
        ["sA"] = new() { Name = "Relax", Type = "GroupScene", Group = "1" },
        ["sB"] = new() { Name = "Bright", Type = "GroupScene", Group = "1" },
        ["sC"] = new() { Name = "Cooking", Type = "GroupScene", Group = "2" },
        ["sD"] = new() { Name = "Movie", Type = "GroupScene", Group = "3" },
        ["sE"] = new() { Name = "Legacy Light Scene", Type = "LightScene", Lights = { "1" } },
    };

    [Fact]
    public void Rooms_and_zones_included_but_light_groups_excluded()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes(), includeZones: true);

        Assert.Equal(3, rooms.Count);
        Assert.DoesNotContain(rooms, r => r.Name == "TV Backlight");
    }

    [Fact]
    public void Rooms_are_sorted_alphabetically()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        Assert.Equal(new[] { "Downstairs", "Kitchen", "Living Room" }, rooms.Select(r => r.Name));
    }

    [Fact]
    public void Room_carries_its_group_id()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        Assert.Equal("1", rooms.Single(r => r.Name == "Living Room").Id);
    }

    [Fact]
    public void Group_scenes_attach_to_their_group_and_sort_by_name()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        var living = rooms.Single(r => r.Name == "Living Room");
        Assert.Equal(new[] { "Bright", "Relax" }, living.Scenes.Select(s => s.Name));
    }

    [Fact]
    public void Each_room_gets_only_its_own_scenes()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        Assert.Equal(new[] { "Cooking" }, rooms.Single(r => r.Name == "Kitchen").Scenes.Select(s => s.Name));
        Assert.Equal(new[] { "Movie" }, rooms.Single(r => r.Name == "Downstairs").Scenes.Select(s => s.Name));
    }

    [Fact]
    public void Light_scenes_are_never_attached_to_a_room()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        Assert.All(rooms, r => Assert.DoesNotContain(r.Scenes, s => s.Name == "Legacy Light Scene"));
    }

    [Fact]
    public void IncludeZones_false_drops_zones()
    {
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes(), includeZones: false);

        Assert.Equal(2, rooms.Count);
        Assert.DoesNotContain(rooms, r => r.Name == "Downstairs");
    }

    [Fact]
    public void IncludeZones_defaults_to_true()
    {
        // The tray defaults to showing zones; guard that the optional parameter's default
        // stays true so a caller that omits it keeps seeing zones.
        var rooms = RoomSceneMapper.BuildRooms(SampleGroups(), SampleScenes());

        Assert.Contains(rooms, r => r.Name == "Downstairs");
    }

    [Theory]
    [InlineData("room")]
    [InlineData("ROOM")]
    [InlineData("Room")]
    public void Group_type_matching_is_case_insensitive(string type)
    {
        var groups = new Dictionary<string, HueGroup> { ["1"] = new() { Name = "Den", Type = type } };

        var rooms = RoomSceneMapper.BuildRooms(groups, new Dictionary<string, HueScene>());

        Assert.Single(rooms);
        Assert.Equal("Den", rooms[0].Name);
    }

    [Fact]
    public void GroupScene_type_matching_is_case_insensitive()
    {
        var groups = new Dictionary<string, HueGroup> { ["1"] = new() { Name = "Den", Type = "Room" } };
        var scenes = new Dictionary<string, HueScene>
        {
            ["s1"] = new() { Name = "Chill", Type = "groupscene", Group = "1" },
        };

        var rooms = RoomSceneMapper.BuildRooms(groups, scenes);

        Assert.Equal(new[] { "Chill" }, rooms.Single().Scenes.Select(s => s.Name));
    }

    [Fact]
    public void A_scene_whose_group_matches_no_group_is_dropped()
    {
        var groups = new Dictionary<string, HueGroup> { ["1"] = new() { Name = "Den", Type = "Room" } };
        var scenes = new Dictionary<string, HueScene>
        {
            ["s1"] = new() { Name = "Orphan", Type = "GroupScene", Group = "999" },
        };

        var rooms = RoomSceneMapper.BuildRooms(groups, scenes);

        Assert.Empty(rooms.Single().Scenes);
    }

    [Fact]
    public void Empty_groups_yield_no_rooms()
    {
        var rooms = RoomSceneMapper.BuildRooms(
            new Dictionary<string, HueGroup>(), new Dictionary<string, HueScene>());

        Assert.Empty(rooms);
    }

    [Fact]
    public void A_room_with_no_matching_scenes_still_appears_with_an_empty_scene_list()
    {
        // group id "77" is referenced by none of SampleScenes' scenes.
        var groups = new Dictionary<string, HueGroup> { ["77"] = new() { Name = "Empty Room", Type = "Room" } };

        var rooms = RoomSceneMapper.BuildRooms(groups, SampleScenes());

        Assert.Empty(rooms.Single().Scenes);
    }
}
