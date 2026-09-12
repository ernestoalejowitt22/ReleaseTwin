using System.Net;
using System.Text;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// Stands in for the public, credential-free endpoints the shipped examples under examples/cases
/// point at (httpbin.org, jsonplaceholder.typicode.com), so the example sweep never depends on the
/// public internet being up. Mirrors each endpoint's real contract closely enough for the example
/// cases to exercise capture and bearer forwarding; any other URL throws so a new example that
/// silently reaches the network fails loudly here instead of flaking in CI.
/// </summary>
public sealed class FakePublicHttpHandler : HttpMessageHandler
{
    public const string IssuedUuid = "9d2c1a4e-3b7f-4e8a-9c0d-5f6a7b8c9d0e";

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var uri = request.RequestUri!;

        if (uri.Host == "httpbin.org" && uri.AbsolutePath == "/uuid" && request.Method == HttpMethod.Get)
        {
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"uuid\": \"" + IssuedUuid + "\"}"));
        }

        if (uri.Host == "httpbin.org" && uri.AbsolutePath == "/bearer")
        {
            // Real httpbin.org/bearer: 200 with the token echoed back when a "Bearer <token>"
            // Authorization header is present, 401 otherwise.
            var auth = request.Headers.Authorization;
            return Task.FromResult(auth is { Scheme: "Bearer", Parameter.Length: > 0 }
                ? JsonResponse(HttpStatusCode.OK, "{\"authenticated\": true, \"token\": \"" + auth.Parameter + "\"}")
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        if (uri.Host == "jsonplaceholder.typicode.com" && uri.AbsolutePath == "/posts/1" && request.Method == HttpMethod.Get)
        {
            return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                "{\"userId\": 1, \"id\": 1, \"title\": \"example post\", \"body\": \"example body\"}"));
        }

        throw new InvalidOperationException(
            $"Unhandled fake public HTTP request: {request.Method} {uri}. Add it to {nameof(FakePublicHttpHandler)} " +
            "or move the example out of examples/cases so the offline sweep does not run it.");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
