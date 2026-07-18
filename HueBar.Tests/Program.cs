using System.Text.Json;
using HueBar.Core;

// A dependency-free test runner: each Check prints PASS/FAIL and the process exits non-zero
// if anything failed, so `dotnet run` doubles as a CI-style gate on the core mapping logic.

int failures = 0;
void Check(bool condition, string name)
{
    Console.WriteLine((condition ? "PASS" : "FAIL") + ": " + name);
    if (!condition) failures++;
}

// ---------------------------------------------------------------------------
// 1. Mapping from typed dictionaries
// ---------------------------------------------------------------------------
var groups = new Dictionary<string, HueGroup>
{
    ["1"] = new() { Name = "Living Room", Type = "Room", Lights = { "1", "2" } },
    ["2"] = new() { Name = "Kitchen", Type = "Room", Lights = { "3" } },
    ["3"] = new() { Name = "Downstairs", Type = "Zone", Lights = { "1", "3" } },
    ["9"] = new() { Name = "TV Backlight", Type = "LightGroup", Lights = { "4" } },
};
var scenes = new Dictionary<string, HueScene>
{
    ["sA"] = new() { Name = "Relax", Type = "GroupScene", Group = "1" },
    ["sB"] = new() { Name = "Bright", Type = "GroupScene", Group = "1" },
    ["sC"] = new() { Name = "Cooking", Type = "GroupScene", Group = "2" },
    ["sD"] = new() { Name = "Movie", Type = "GroupScene", Group = "3" },
    ["sE"] = new() { Name = "Legacy Light Scene", Type = "LightScene", Lights = { "1" } },
};

var rooms = RoomSceneMapper.BuildRooms(groups, scenes, includeZones: true);

Check(rooms.Count == 3, "3 entries returned (2 Rooms + 1 Zone; LightGroup excluded)");
Check(rooms[0].Name == "Downstairs" && rooms[1].Name == "Kitchen" && rooms[2].Name == "Living Room",
    "rooms are sorted alphabetically");

var living = rooms.Single(r => r.Name == "Living Room");
Check(living.Id == "1", "Living Room carries its group id");
Check(living.Scenes.Count == 2, "Living Room has 2 scenes");
Check(living.Scenes[0].Name == "Bright" && living.Scenes[1].Name == "Relax",
    "Living Room scenes sorted alphabetically");

var kitchen = rooms.Single(r => r.Name == "Kitchen");
Check(kitchen.Scenes.Count == 1 && kitchen.Scenes[0].Name == "Cooking", "Kitchen has only its own scene");

var zone = rooms.Single(r => r.Name == "Downstairs");
Check(zone.Scenes.Count == 1 && zone.Scenes[0].Name == "Movie", "Zone gets its GroupScene");

Check(rooms.All(r => r.Scenes.All(s => s.Name != "Legacy Light Scene")),
    "LightScene is not attached to any room");
Check(rooms.All(r => r.Name != "TV Backlight"), "LightGroup is excluded from rooms");

// includeZones = false
var roomsOnly = RoomSceneMapper.BuildRooms(groups, scenes, includeZones: false);
Check(roomsOnly.Count == 2 && roomsOnly.All(r => r.Name != "Downstairs"),
    "includeZones=false drops the Zone");

// ---------------------------------------------------------------------------
// 2. End-to-end: real bridge JSON shapes → models → mapper
//    (guards the [JsonPropertyName] attributes and dictionary keying)
// ---------------------------------------------------------------------------
const string groupsJson = """
{
  "1": { "name": "Lounge", "lights": ["9","1","2"], "type": "Room", "class": "Lounge", "action": { "on": false, "bri": 254 } },
  "3": { "name": "Downstairs", "lights": ["9","1"], "type": "Zone", "class": "Other" },
  "7": { "name": "Ad hoc", "lights": ["5"], "type": "LightGroup" }
}
""";
const string scenesJson = """
{
  "8AuCtLbIiEJJRNB": { "name": "Nightlight", "type": "GroupScene", "group": "1", "lights": ["2"], "recycle": false },
  "abc": { "name": "Energize", "type": "GroupScene", "group": "1", "lights": ["9","1","2"] },
  "def": { "name": "Zone Chill", "type": "GroupScene", "group": "3", "lights": ["9","1"] },
  "ghi": { "name": "Legacy", "type": "LightScene", "lights": ["1"] }
}
""";

var parsedGroups = JsonSerializer.Deserialize<Dictionary<string, HueGroup>>(groupsJson)!;
var parsedScenes = JsonSerializer.Deserialize<Dictionary<string, HueScene>>(scenesJson)!;

Check(parsedGroups["1"].Name == "Lounge" && parsedGroups["1"].Type == "Room" && parsedGroups["1"].Class == "Lounge",
    "group JSON fields (name/type/class) parse");
Check(parsedGroups["1"].Lights.Count == 3, "group lights array parses");
Check(parsedScenes["8AuCtLbIiEJJRNB"].Group == "1" && parsedScenes["8AuCtLbIiEJJRNB"].Type == "GroupScene",
    "scene group/type fields parse");

var fromJson = RoomSceneMapper.BuildRooms(parsedGroups, parsedScenes, includeZones: true);
var lounge = fromJson.Single(r => r.Name == "Lounge");
Check(lounge.Scenes.Count == 2 && lounge.Scenes[0].Name == "Energize" && lounge.Scenes[1].Name == "Nightlight",
    "Lounge has its 2 GroupScenes, sorted");
Check(fromJson.Single(r => r.Name == "Downstairs").Scenes.Single().Name == "Zone Chill",
    "Zone from JSON gets its scene");
Check(fromJson.All(r => r.Name != "Ad hoc"), "LightGroup from JSON excluded");

// ---------------------------------------------------------------------------
// 3. Discovery + pairing error models
// ---------------------------------------------------------------------------
var discovered = JsonSerializer.Deserialize<List<DiscoveredBridge>>(
    """[ { "id": "001788fffe123456", "internalipaddress": "192.168.0.14", "port": 443 } ]""")!;
Check(discovered.Count == 1 && discovered[0].InternalIpAddress == "192.168.0.14",
    "discovery JSON parses internalipaddress");

var linkErr = JsonSerializer.Deserialize<List<HueApiResponse>>(
    """[ { "error": { "type": 101, "address": "", "description": "link button not pressed" } } ]""")!;
Check(linkErr[0].Error is { Type: 101 }, "link-button-not-pressed error (type 101) parses");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
return failures == 0 ? 0 : 1;
