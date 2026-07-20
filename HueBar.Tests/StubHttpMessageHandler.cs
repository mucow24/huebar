using System.Net;

namespace HueBar.Tests;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> so <see cref="HueBar.Core.HueClient"/> can be
/// exercised end-to-end against canned bridge responses, and so tests can assert on the
/// exact request (method, URL, body) the client sent.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

    /// <summary>Every request the handler saw, with its body already read, in order.</summary>
    public List<CapturedRequest> Requests { get; } = new();

    private StubHttpMessageHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>Always replies with the given body (HTTP 200 by default).</summary>
    public static StubHttpMessageHandler AlwaysReturns(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new((_, _) => Respond(body, status));

    /// <summary>Replies with a different body on each successive call (for retry-loop tests).</summary>
    public static StubHttpMessageHandler ReturnsInSequence(params string[] bodies)
    {
        int i = 0;
        return new((_, _) =>
        {
            var body = bodies[Math.Min(i, bodies.Length - 1)];
            i++;
            return Respond(body, HttpStatusCode.OK);
        });
    }

    /// <summary>Throws, simulating a network failure / unreachable host.</summary>
    public static StubHttpMessageHandler AlwaysThrows()
        => new((_, _) => throw new HttpRequestException("simulated network failure"));

    /// <summary>Full control: decide the response from the request and its (already-read) body.</summary>
    public static StubHttpMessageHandler From(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
        => new(responder);

    private static HttpResponseMessage Respond(string body, HttpStatusCode status)
        => new(status) { Content = new StringContent(body) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
        cancellationToken.ThrowIfCancellationRequested();
        return _responder(request, body);
    }

    public HueBar.Core.HueClient NewClient() => new(new HttpClient(this));

    internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
