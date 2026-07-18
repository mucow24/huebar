namespace HueBar.Core;

/// <summary>A room (or zone) with its associated scenes, ready to render as a tray submenu.</summary>
public sealed class Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public List<SceneRef> Scenes { get; } = new();
}

public sealed class SceneRef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Pure logic: turns the bridge's raw groups + scenes dictionaries into a sorted list of
/// rooms, each carrying the scenes that belong to it. This is the unit-tested core of the app.
/// </summary>
public static class RoomSceneMapper
{
    /// <param name="groups">GET /groups payload, keyed by group id.</param>
    /// <param name="scenes">GET /scenes payload, keyed by scene id.</param>
    /// <param name="includeZones">When true, Zones are listed alongside Rooms.</param>
    public static List<Room> BuildRooms(
        IReadOnlyDictionary<string, HueGroup> groups,
        IReadOnlyDictionary<string, HueScene> scenes,
        bool includeZones = true)
    {
        var rooms = new List<Room>();

        foreach (var (groupId, group) in groups)
        {
            bool isRoom = string.Equals(group.Type, "Room", StringComparison.OrdinalIgnoreCase);
            bool isZone = string.Equals(group.Type, "Zone", StringComparison.OrdinalIgnoreCase);
            if (!isRoom && !(includeZones && isZone))
                continue;

            var room = new Room { Id = groupId, Name = group.Name };

            // A scene belongs to this room when it is a GroupScene whose "group" is this group id.
            foreach (var (sceneId, scene) in scenes)
            {
                bool belongs =
                    string.Equals(scene.Type, "GroupScene", StringComparison.OrdinalIgnoreCase)
                    && scene.Group == groupId;
                if (belongs)
                    room.Scenes.Add(new SceneRef { Id = sceneId, Name = scene.Name });
            }

            room.Scenes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            rooms.Add(room);
        }

        rooms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rooms;
    }
}
