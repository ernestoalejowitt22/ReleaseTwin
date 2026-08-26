using System.Net;
using System.Net.Http.Json;

namespace ReleaseTwin.Cli.Upload;

/// <summary>Thrown when a project has no stored credentials for the requested adapter — distinct from an auth/network failure.</summary>
public sealed class AdapterCredentialNotConfiguredException : Exception
{
}

/// <summary>Thrown for any other fetch failure (network, unexpected status, malformed response).</summary>
public sealed class AdapterCredentialFetchException : Exception
{
    public AdapterCredentialFetchException(string message) : base(message)
    {
    }
}

/// <summary>
/// hosted-adapter-credentials: fetches a project's stored credentials for one adapter. Same shape as
/// JourneyFetchClient/IngestClient (independently-defined DTO, injectable handler for testing).
/// </summary>
public sealed class AdapterCredentialsClient : IDisposable
{
    private readonly HttpClient _client;

    public AdapterCredentialsClient(string baseUrl, string apiToken, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> FetchAsync(string adapter, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync($"/api/cli/adapter-credentials/{adapter}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AdapterCredentialNotConfiguredException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AdapterCredentialFetchException($"HTTP {(int)response.StatusCode} fetching '{adapter}' credentials: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<AdapterCredentialPayload>(cancellationToken: cancellationToken)
            ?? throw new AdapterCredentialFetchException($"'{adapter}' credentials: empty response body");
        return payload.Fields;
    }

    public void Dispose() => _client.Dispose();

    // Independently defined from the hosted API's own AdapterCredentialResponse DTO — same
    // deliberate-decoupling convention IngestClient/JourneyFetchClient's payloads already follow.
    private sealed class AdapterCredentialPayload
    {
        public string Adapter { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}
