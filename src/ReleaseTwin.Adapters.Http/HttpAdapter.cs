using ReleaseTwin.AdapterSdk;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// A vendor-neutral HTTP adapter: any REST API is testable from case-file data alone, no bespoke
/// adapter code required per target. Requires no configuration to install (specs/http-adapter.md).
/// </summary>
public sealed class HttpAdapter : IAdapterModule, IDisposable
{
    private readonly HttpClient _client;

    public HttpAdapter(HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true);
    }

    public string Name => "http";

    /// <summary>
    /// http-flag-control: the adapter's own <see cref="HttpClient"/>, so a CLI-built
    /// <see cref="HttpFeatureStateController"/> for a case's <c>flag_proof.control</c> block shares
    /// the same handler (and the test handler injected via the ctor).
    /// </summary>
    public HttpClient HttpClient => _client;

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddOperation("http.request", new HttpRequestOperation(_client))
            .AddOperation("http.assertJsonPath", new JsonPathAssertOperation())
            .AddOperation("http.oauth2ClientCredentials", new Oauth2ClientCredentialsOperation(_client))
            .AddCapability("http:generic")
            // http-flag-control (design D3): registered unconditionally. The real gate is the CLI,
            // which only builds a controller when a case declares `flag_proof.control` or an adapter
            // vends one; the HTTP adapter itself does not implement IFeatureStateControllerSource
            // because its controllers are per-case, not composition-wide.
            .AddCapability("flag-control:runtime");
    }

    public void Dispose() => _client.Dispose();
}
