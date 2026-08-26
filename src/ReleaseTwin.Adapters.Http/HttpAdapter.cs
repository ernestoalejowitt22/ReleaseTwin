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

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddOperation("http.request", new HttpRequestOperation(_client))
            .AddOperation("http.assertJsonPath", new JsonPathAssertOperation())
            .AddOperation("http.oauth2ClientCredentials", new Oauth2ClientCredentialsOperation(_client))
            .AddCapability("http:generic");
    }

    public void Dispose() => _client.Dispose();
}
