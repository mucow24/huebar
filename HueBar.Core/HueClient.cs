using System.Text;
using System.Text.Json;

namespace HueBar.Core;

/// <summary>
/// Thin async client for the Philips Hue local API v1 (CLIP over plain HTTP).
/// All calls are best-effort and surface failures as exceptions or typed results.
/// </summary>
public sealed class HueClient
{
    private readonly HttpClient _http;

    public HueClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>Cloud discovery. Returns an empty list on any failure (network, rate-limit, none found).</summary>
    public async Task<List<DiscoveredBridge>> DiscoverBridgesAsync(CancellationToken ct = default)
    {
        try
        {
            var body = await _http.GetStringAsync("https://discovery.meethue.com/", ct);
            return JsonSerializer.Deserialize<List<DiscoveredBridge>>(body) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Attempts to create an application key. The physical link button on the bridge must be
    /// pressed within ~30s beforehand; otherwise the bridge replies with error type 101 and
    /// <see cref="PairResult.LinkButtonNotPressed"/> is true (caller should retry).
    /// </summary>
    public async Task<PairResult> PairAsync(string bridgeIp, string appName = "huebar", string deviceName = "windows", CancellationToken ct = default)
    {
        var url = $"http://{bridgeIp}/api";
        var payload = JsonSerializer.Serialize(new { devicetype = $"{appName}#{deviceName}" });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        var items = JsonSerializer.Deserialize<List<HueApiResponse>>(body);
        var first = items is { Count: > 0 } ? items[0] : null;

        if (first?.Success is { } success && success.TryGetValue("username", out var u))
            return PairResult.Ok(u.GetString() ?? "");
        if (first?.Error is { } error)
            return PairResult.Fail(error.Type, error.Description ?? "Unknown bridge error.");
        return PairResult.Fail(-1, "Unexpected response from bridge.");
    }

    public async Task<Dictionary<string, HueGroup>> GetGroupsAsync(string bridgeIp, string username, CancellationToken ct = default)
    {
        var body = await _http.GetStringAsync($"http://{bridgeIp}/api/{username}/groups", ct);
        return ParseObjectOrThrow<Dictionary<string, HueGroup>>(body);
    }

    public async Task<Dictionary<string, HueScene>> GetScenesAsync(string bridgeIp, string username, CancellationToken ct = default)
    {
        var body = await _http.GetStringAsync($"http://{bridgeIp}/api/{username}/scenes", ct);
        return ParseObjectOrThrow<Dictionary<string, HueScene>>(body);
    }

    /// <summary>Recalls a scene onto a group. Returns true when the bridge acknowledges success.</summary>
    public Task<bool> ActivateSceneAsync(string bridgeIp, string username, string groupId, string sceneId, CancellationToken ct = default)
        => SendGroupActionAsync(bridgeIp, username, groupId, new { scene = sceneId }, ct);

    /// <summary>Turns every light in a group (room/zone) off. Returns true on success.</summary>
    public Task<bool> TurnGroupOffAsync(string bridgeIp, string username, string groupId, CancellationToken ct = default)
        => SendGroupActionAsync(bridgeIp, username, groupId, new { on = false }, ct);

    /// <summary>
    /// PUTs an action object to /groups/&lt;id&gt;/action (e.g. {"scene":"..."} or {"on":false}).
    /// Throws <see cref="HueApiException"/> if the bridge returns an error element.
    /// </summary>
    private async Task<bool> SendGroupActionAsync(string bridgeIp, string username, string groupId, object action, CancellationToken ct)
    {
        var url = $"http://{bridgeIp}/api/{username}/groups/{groupId}/action";
        var payload = JsonSerializer.Serialize(action);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PutAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        // Success shape: [{"success":{"/groups/<id>/action/<key>":<value>}}]
        var items = JsonSerializer.Deserialize<List<HueApiResponse>>(body);
        var first = items is { Count: > 0 } ? items[0] : null;
        if (first?.Error is { } error)
            throw new HueApiException(error.Type, error.Description ?? "Bridge rejected the command.");
        return resp.IsSuccessStatusCode && first?.Success is not null;
    }

    /// <summary>
    /// Deserializes a JSON object payload. If the bridge instead returned an error array
    /// (e.g. "unauthorized user"), throws a <see cref="HueApiException"/> rather than a raw
    /// JSON exception, so callers can show a friendly message.
    /// </summary>
    private static T ParseObjectOrThrow<T>(string body)
    {
        if (body.TrimStart().StartsWith('['))
        {
            var errors = JsonSerializer.Deserialize<List<HueApiResponse>>(body);
            var error = errors is { Count: > 0 } ? errors[0].Error : null;
            throw new HueApiException(error?.Type ?? -1, error?.Description ?? "Bridge returned an error.");
        }
        return JsonSerializer.Deserialize<T>(body) ?? throw new HueApiException(-1, "Empty response from bridge.");
    }
}
