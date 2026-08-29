using System.Net;
using System.Net.Http.Json;

namespace ReleaseTwin.Cli.Upload;

/// <summary>Thrown for any failure fetching the hosted per-project evidence config (network, unexpected status, malformed).</summary>
public sealed class EvidenceConfigFetchException : Exception
{
    public EvidenceConfigFetchException(string message) : base(message)
    {
    }
}

public sealed record EvidenceConfig(bool CaptureDefault, int RetentionDays);

/// <summary>
/// evidence-capture (cli-runner delta): fetches the token's project's evidence config — whether
/// capture is on by default and the retention window — the same client shape as
/// <see cref="AdapterCredentialsClient"/>. Token-scoped: the project is identified by the API token.
/// </summary>
public sealed class EvidenceConfigClient : IDisposable
{
    private readonly HttpClient _client;

    public EvidenceConfigClient(string baseUrl, string apiToken, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<EvidenceConfig> FetchAsync(CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync("/api/cli/evidence-config", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = response.StatusCode == HttpStatusCode.NotFound
                ? "not found"
                : await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EvidenceConfigFetchException($"HTTP {(int)response.StatusCode} fetching evidence config: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<Payload>(cancellationToken: cancellationToken)
            ?? throw new EvidenceConfigFetchException("evidence config: empty response body");
        return new EvidenceConfig(payload.CaptureDefault, payload.RetentionDays);
    }

    public void Dispose() => _client.Dispose();

    private sealed class Payload
    {
        public bool CaptureDefault { get; set; }
        public int RetentionDays { get; set; }
    }
}
