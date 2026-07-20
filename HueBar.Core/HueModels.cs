using System.Text.Json;
using System.Text.Json.Serialization;

namespace HueBar.Core;

/// <summary>A bridge returned by the cloud discovery endpoint (https://discovery.meethue.com/).</summary>
public sealed class DiscoveredBridge
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("internalipaddress")] public string? InternalIpAddress { get; set; }
    [JsonPropertyName("port")] public int Port { get; set; }
}

/// <summary>
/// The subset of a bridge's <c>/config</c> HueBar uses to label and identify it: the user-set
/// <c>name</c> and the immutable <c>bridgeid</c>. Other config fields are ignored.
/// </summary>
public sealed class BridgeConfig
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("bridgeid")] public string? BridgeId { get; set; }
}

/// <summary>A Hue "group" — Room, Zone, LightGroup, etc. Returned keyed by id from GET /groups.</summary>
public sealed class HueGroup
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("class")] public string? Class { get; set; }
    [JsonPropertyName("lights")] public List<string> Lights { get; set; } = new();
}

/// <summary>A Hue scene. GroupScenes carry a <see cref="Group"/> id linking them to a room/zone.</summary>
public sealed class HueScene
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("group")] public string? Group { get; set; }
    [JsonPropertyName("lights")] public List<string> Lights { get; set; } = new();
}

/// <summary>One element of the array the bridge returns for POST /api and PUT actions.</summary>
public sealed class HueApiResponse
{
    [JsonPropertyName("success")] public Dictionary<string, JsonElement>? Success { get; set; }
    [JsonPropertyName("error")] public HueError? Error { get; set; }
}

public sealed class HueError
{
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

/// <summary>Raised when the bridge returns an error array (e.g. unauthorized user) where an object was expected.</summary>
public sealed class HueApiException : Exception
{
    public int ErrorType { get; }
    public HueApiException(int errorType, string message) : base(message) => ErrorType = errorType;
}

/// <summary>Result of a pairing attempt. <see cref="LinkButtonNotPressed"/> means "retry after pressing".</summary>
public sealed class PairResult
{
    public bool Success { get; private init; }
    public string? Username { get; private init; }
    public int ErrorType { get; private init; }
    public string? ErrorMessage { get; private init; }

    public bool LinkButtonNotPressed => ErrorType == 101;

    public static PairResult Ok(string username) => new() { Success = true, Username = username };
    public static PairResult Fail(int type, string message) => new() { Success = false, ErrorType = type, ErrorMessage = message };
}
