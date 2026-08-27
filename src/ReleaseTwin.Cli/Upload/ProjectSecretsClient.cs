using System.Net.Http.Json;

namespace ReleaseTwin.Cli.Upload;

/// <summary>Thrown for a project-secrets fetch failure (network, unexpected status, malformed response).</summary>
public sealed class ProjectSecretsFetchException : Exception
{
    public ProjectSecretsFetchException(string message) : base(message)
    {
    }
}

/// <summary>
/// hosted-project-secrets: fetches a project's full set of stored secrets in one call. Same shape as
/// AdapterCredentialsClient/JourneyFetchClient (independently-defined DTO, injectable handler for
/// testing) — unlike AdapterCredentialsClient, there's no "not configured" 404 case here: a project
/// with nothing stored is a 200 with an empty set, per project-secrets' own spec.
/// </summary>
public sealed class ProjectSecretsClient : IDisposable
{
    private readonly HttpClient _client;

    public ProjectSecretsClient(string baseUrl, string apiToken, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> FetchAllAsync(CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync("/api/cli/project-secrets", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProjectSecretsFetchException($"HTTP {(int)response.StatusCode} fetching project secrets: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<ProjectSecretsPayload>(cancellationToken: cancellationToken)
            ?? throw new ProjectSecretsFetchException("project secrets: empty response body");
        return payload.Secrets;
    }

    public void Dispose() => _client.Dispose();

    // Independently defined from the hosted API's own ProjectSecretsResponse DTO — same
    // deliberate-decoupling convention every other cross-boundary payload here follows.
    private sealed class ProjectSecretsPayload
    {
        public Dictionary<string, string> Secrets { get; set; } = new();
    }
}
