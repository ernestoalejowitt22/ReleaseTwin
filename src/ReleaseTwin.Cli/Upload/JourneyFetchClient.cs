using System.Net.Http.Json;

namespace ReleaseTwin.Cli.Upload;

/// <summary>Thrown when a hosted journey version cannot be fetched — network failure, invalid auth, or the version doesn't exist.</summary>
public sealed class JourneyFetchException : Exception
{
    public JourneyFetchException(string message) : base(message)
    {
    }
}

/// <summary>
/// hosted-journeys: fetches one pinned journey version from the hosted API. Same shape as
/// IngestClient (independently-defined DTO, injectable handler for testing) but in the opposite
/// direction — this is the first hosted capability the CLI fetches something to *execute* from,
/// rather than only reports to.
/// </summary>
public sealed class JourneyFetchClient : IDisposable
{
    private readonly HttpClient _client;

    public JourneyFetchClient(string baseUrl, string apiToken, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<string> FetchJourneyVersionAsync(Guid journeyId, int version, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync($"/api/cli/journeys/{journeyId}/versions/{version}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new JourneyFetchException($"HTTP {(int)response.StatusCode} fetching journey {journeyId} version {version}: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JourneyVersionPayload>(cancellationToken: cancellationToken)
            ?? throw new JourneyFetchException($"journey {journeyId} version {version}: empty response body");

        return payload.YamlContent;
    }

    public void Dispose() => _client.Dispose();

    // Independently defined from the hosted API's own JourneyVersionResponse DTO — same
    // deliberate-decoupling convention IngestClient's payloads already follow.
    private sealed class JourneyVersionPayload
    {
        public Guid JourneyId { get; set; }
        public int Version { get; set; }
        public string YamlContent { get; set; } = "";
    }
}
