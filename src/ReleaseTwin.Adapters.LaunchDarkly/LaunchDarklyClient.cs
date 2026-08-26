using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ReleaseTwin.Adapters.LaunchDarkly;

/// <summary>
/// Thin wrapper over LaunchDarkly's REST API (flags). Auth and base address are set once at
/// construction; every call is a plain HTTP request so a test can substitute a fake
/// <see cref="HttpMessageHandler"/> without touching this class.
/// </summary>
public sealed class LaunchDarklyClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _projectKey;
    private readonly string _environmentKey;

    public LaunchDarklyClient(LaunchDarklyOptions options, HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri("https://app.launchdarkly.com/"),
        };

        // LaunchDarkly's API key goes in a raw `Authorization` header — not `Bearer`/`Basic`.
        _http.DefaultRequestHeaders.Add("Authorization", options.ApiToken);
        _projectKey = options.ProjectKey;
        _environmentKey = options.EnvironmentKey;
    }

    public async Task<bool?> GetFlagStateAsync(string flagKey, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            $"api/v2/flags/{Uri.EscapeDataString(_projectKey)}/{Uri.EscapeDataString(flagKey)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return body?["environments"]?[_environmentKey]?["on"]?.GetValue<bool>();
    }

    public async Task SetFlagStateAsync(string flagKey, bool enabled, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"api/v2/flags/{Uri.EscapeDataString(_projectKey)}/{Uri.EscapeDataString(flagKey)}")
        {
            Content = JsonContent.Create(new[]
            {
                new { op = "replace", path = $"/environments/{_environmentKey}/on", value = enabled },
            }),
        };
        request.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
