using System.Net;

namespace HueBar.Tests;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> so <see cref="HueBar.Core.HueClient"/> can be
/// exercised end-to-end against canned bridge responses, and so tests can assert on the
/// exact request (method, URL, body) the client sent.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _responder;

    /// <summary>Every request the handler saw, with its body already read, in order.</summary>
    public List<CapturedRequest> Requests { get; } = new();

    private StubHttpMessageHandler(Func<HttpResponseMessage> responder) => _responder = responder;

    /// <summary>Always replies with the given body (HTTP 200 by default). A fresh response per
    /// request, because the client disposes each one it receives.</summary>
    public static StubHttpMessageHandler AlwaysReturns(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(() => new HttpResponseMessage(status) { Content = new StringContent(body) });

    /// <summary>Throws, simulating a network failure / unreachable host.</summary>
    public static StubHttpMessageHandler AlwaysThrows()
        => new(() => throw new HttpRequestException("simulated network failure"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
        cancellationToken.ThrowIfCancellationRequested();
        return _responder();
    }

    public HueBar.Core.HueClient NewClient() => new(new HttpClient(this));

    internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
