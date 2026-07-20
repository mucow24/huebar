using System.Net;
using System.Text.Json;
using HueBar.Core;

namespace HueBar.Tests;

public class HueClientTests
{
    // ---- DiscoverBridgesAsync -------------------------------------------------

    [Fact]
    public async Task Discover_parses_the_bridge_list_and_calls_the_cloud_endpoint()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "id": "001788fffe123456", "internalipaddress": "192.168.0.14", "port": 443 } ]""");
        var client = stub.NewClient();

        var bridges = await client.DiscoverBridgesAsync();

        var bridge = Assert.Single(bridges);
        Assert.Equal("001788fffe123456", bridge.Id);
        Assert.Equal("192.168.0.14", bridge.InternalIpAddress);
        Assert.Equal(443, bridge.Port);
        Assert.Equal("https://discovery.meethue.com/", stub.Requests.Single().Uri.ToString());
    }

    [Fact]
    public async Task Discover_returns_empty_on_network_failure()
    {
        var client = StubHttpMessageHandler.AlwaysThrows().NewClient();

        var bridges = await client.DiscoverBridgesAsync();

        Assert.Empty(bridges);
    }

    [Fact]
    public async Task Discover_returns_empty_when_body_is_json_null()
    {
        var client = StubHttpMessageHandler.AlwaysReturns("null").NewClient();

        Assert.Empty(await client.DiscoverBridgesAsync());
    }

    // ---- PairAsync ------------------------------------------------------------

    [Fact]
    public async Task Pair_returns_the_username_on_success()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns("""[ { "success": { "username": "abc123KEY" } } ]""");
        var client = stub.NewClient();

        var result = await client.PairAsync("10.0.0.5");

        Assert.True(result.Success);
        Assert.Equal("abc123KEY", result.Username);
        Assert.False(result.LinkButtonNotPressed);
    }

    [Fact]
    public async Task Pair_posts_the_devicetype_to_the_bridge_api()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns("""[ { "success": { "username": "k" } } ]""");
        var client = stub.NewClient();

        await client.PairAsync("10.0.0.5", appName: "huebar", deviceName: "windows");

        var req = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("http://10.0.0.5/api", req.Uri.ToString());
        using var doc = JsonDocument.Parse(req.Body);
        Assert.Equal("huebar#windows", doc.RootElement.GetProperty("devicetype").GetString());
    }

    [Fact]
    public async Task Pair_reports_link_button_not_pressed_on_error_type_101()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "error": { "type": 101, "address": "", "description": "link button not pressed" } } ]""");
        var client = stub.NewClient();

        var result = await client.PairAsync("10.0.0.5");

        Assert.False(result.Success);
        Assert.True(result.LinkButtonNotPressed);
        Assert.Equal(101, result.ErrorType);
        Assert.Equal("link button not pressed", result.ErrorMessage);
    }

    [Fact]
    public async Task Pair_surfaces_other_bridge_errors_without_flagging_link_button()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "error": { "type": 7, "address": "/", "description": "invalid value" } } ]""");
        var client = stub.NewClient();

        var result = await client.PairAsync("10.0.0.5");

        Assert.False(result.Success);
        Assert.False(result.LinkButtonNotPressed);
        Assert.Equal(7, result.ErrorType);
    }

    [Fact]
    public async Task Pair_returns_a_failure_for_an_unexpected_empty_response()
    {
        var client = StubHttpMessageHandler.AlwaysReturns("[]").NewClient();

        var result = await client.PairAsync("10.0.0.5");

        Assert.False(result.Success);
        Assert.Equal(-1, result.ErrorType);
    }

    // ---- GetGroupsAsync / GetScenesAsync -------------------------------------

    [Fact]
    public async Task GetGroups_parses_the_keyed_object_and_targets_the_right_url()
    {
        const string body = """
        { "1": { "name": "Lounge", "type": "Room", "class": "Lounge", "lights": ["1","2"] } }
        """;
        var stub = StubHttpMessageHandler.AlwaysReturns(body);
        var client = stub.NewClient();

        var groups = await client.GetGroupsAsync("10.0.0.5", "USERKEY");

        Assert.Equal("Lounge", groups["1"].Name);
        Assert.Equal("Room", groups["1"].Type);
        Assert.Equal(2, groups["1"].Lights.Count);
        Assert.Equal("http://10.0.0.5/api/USERKEY/groups", stub.Requests.Single().Uri.ToString());
    }

    [Fact]
    public async Task GetScenes_parses_the_keyed_object_and_targets_the_right_url()
    {
        const string body = """
        { "sX": { "name": "Relax", "type": "GroupScene", "group": "1" } }
        """;
        var stub = StubHttpMessageHandler.AlwaysReturns(body);
        var client = stub.NewClient();

        var scenes = await client.GetScenesAsync("10.0.0.5", "USERKEY");

        Assert.Equal("Relax", scenes["sX"].Name);
        Assert.Equal("1", scenes["sX"].Group);
        Assert.Equal("http://10.0.0.5/api/USERKEY/scenes", stub.Requests.Single().Uri.ToString());
    }

    [Fact]
    public async Task GetGroups_throws_a_friendly_error_when_the_bridge_returns_an_error_array()
    {
        // An unauthorized key makes the bridge return an error *array* where an object was expected.
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "error": { "type": 1, "address": "/groups", "description": "unauthorized user" } } ]""");
        var client = stub.NewClient();

        var ex = await Assert.ThrowsAsync<HueApiException>(() => client.GetGroupsAsync("10.0.0.5", "BADKEY"));
        Assert.Equal(1, ex.ErrorType);
        Assert.Equal("unauthorized user", ex.Message);
    }

    [Fact]
    public async Task GetScenes_throws_a_friendly_error_when_the_bridge_returns_an_error_array()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "error": { "type": 1, "address": "/scenes", "description": "unauthorized user" } } ]""");
        var client = stub.NewClient();

        await Assert.ThrowsAsync<HueApiException>(() => client.GetScenesAsync("10.0.0.5", "BADKEY"));
    }

    // ---- ActivateSceneAsync / TurnGroupOffAsync ------------------------------

    [Fact]
    public async Task ActivateScene_puts_the_scene_id_to_the_group_action_url()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "success": { "/groups/1/action/scene": "sX" } } ]""");
        var client = stub.NewClient();

        bool ok = await client.ActivateSceneAsync("10.0.0.5", "USERKEY", groupId: "1", sceneId: "sX");

        Assert.True(ok);
        var req = stub.Requests.Single();
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.Equal("http://10.0.0.5/api/USERKEY/groups/1/action", req.Uri.ToString());
        using var doc = JsonDocument.Parse(req.Body);
        Assert.Equal("sX", doc.RootElement.GetProperty("scene").GetString());
    }

    [Fact]
    public async Task TurnGroupOff_puts_on_false_to_the_group_action_url()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "success": { "/groups/1/action/on": false } } ]""");
        var client = stub.NewClient();

        bool ok = await client.TurnGroupOffAsync("10.0.0.5", "USERKEY", groupId: "1");

        Assert.True(ok);
        var req = stub.Requests.Single();
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.Equal("http://10.0.0.5/api/USERKEY/groups/1/action", req.Uri.ToString());
        using var doc = JsonDocument.Parse(req.Body);
        Assert.False(doc.RootElement.GetProperty("on").GetBoolean());
    }

    [Fact]
    public async Task A_group_action_throws_when_the_bridge_rejects_it()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns(
            """[ { "error": { "type": 3, "address": "/groups/99", "description": "resource not available" } } ]""");
        var client = stub.NewClient();

        var ex = await Assert.ThrowsAsync<HueApiException>(
            () => client.ActivateSceneAsync("10.0.0.5", "USERKEY", "99", "sX"));
        Assert.Equal(3, ex.ErrorType);
    }

    [Fact]
    public async Task A_group_action_returns_false_on_a_non_success_status()
    {
        var stub = StubHttpMessageHandler.AlwaysReturns("[]", HttpStatusCode.InternalServerError);
        var client = stub.NewClient();

        Assert.False(await client.TurnGroupOffAsync("10.0.0.5", "USERKEY", "1"));
    }

    [Fact]
    public async Task A_group_action_returns_false_when_the_bridge_acknowledges_nothing()
    {
        // HTTP 200 but the bridge returned neither a success nor an error element.
        var stub = StubHttpMessageHandler.AlwaysReturns("[ { } ]", HttpStatusCode.OK);
        var client = stub.NewClient();

        Assert.False(await client.ActivateSceneAsync("10.0.0.5", "USERKEY", "1", "sX"));
    }
}
