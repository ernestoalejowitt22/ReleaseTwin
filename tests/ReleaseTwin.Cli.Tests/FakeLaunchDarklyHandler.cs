using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ReleaseTwin.Cli.Tests;

/// <summary>In-memory stand-in for LaunchDarkly's flags API — just enough to satisfy reading/writing a flag's on/off state.</summary>
internal sealed class FakeLaunchDarklyHandler : HttpMessageHandler
{
    private readonly Dictionary<string, bool> _flagState = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Patch && path.StartsWith("/api/v2/flags/", StringComparison.Ordinal))
        {
            var flagKey = path.Split('/')[^1];
            var patch = await request.Content!.ReadFromJsonAsync<JsonNode[]>(cancellationToken: cancellationToken);
            var envKey = patch![0]!["path"]!.GetValue<string>().Split('/')[2];
            _flagState[$"{flagKey}::{envKey}"] = patch[0]!["value"]!.GetValue<bool>();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v2/flags/", StringComparison.Ordinal))
        {
            var flagKey = path.Split('/')[^1];
            var environments = new JsonObject();
            foreach (var (key, on) in _flagState.Where(kv => kv.Key.StartsWith($"{flagKey}::", StringComparison.Ordinal)))
            {
                environments[key.Split("::")[1]] = new JsonObject { ["on"] = on };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new JsonObject { ["environments"] = environments }),
            };
        }

        throw new InvalidOperationException($"unhandled request: {request.Method} {path}");
    }
}
