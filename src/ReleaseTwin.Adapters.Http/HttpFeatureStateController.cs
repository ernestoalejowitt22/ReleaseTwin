using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Raised when a flag-proof control request does not return a 2xx status, or the request cannot be
/// sent at all. The message names the method, URL, and status so the run report can point at the
/// control call as the cause rather than misreporting a leg.
/// </summary>
public sealed class FlagControlException : Exception
{
    public FlagControlException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// http-flag-control: flips an arbitrary feature-flag system's state with one config-declared HTTP
/// request. Built per-case by the CLI (not vended by <see cref="HttpAdapter"/>) from a resolved
/// <c>flag_proof.control</c> block: <c>${ENV_VAR}</c> is already substituted at case-load time;
/// <c>{{featureKey}}</c> / <c>{{state}}</c> / <c>{{enabled}}</c> are substituted here per leg.
/// </summary>
public sealed class HttpFeatureStateController : IFeatureStateController
{
    private readonly HttpClient _client;
    private readonly string _method;
    private readonly string _urlTemplate;
    private readonly IReadOnlyDictionary<string, string> _headerTemplates;
    private readonly string? _bodyTemplate;
    private readonly bool _knownBadWhenDisabled;
    private readonly string _featureKey;

    public HttpFeatureStateController(
        HttpClient client,
        string featureKey,
        string method,
        string urlTemplate,
        IReadOnlyDictionary<string, string> headerTemplates,
        string? bodyTemplate,
        bool knownBadWhenDisabled)
    {
        _client = client;
        _featureKey = featureKey;
        _method = method;
        _urlTemplate = urlTemplate;
        _headerTemplates = headerTemplates;
        _bodyTemplate = bodyTemplate;
        _knownBadWhenDisabled = knownBadWhenDisabled;
    }

    public async Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken)
    {
        // Core drives `enabled: false` for the known-bad leg and `enabled: true` for the known-good
        // leg. Polarity lives here (design D2): with the default `known_bad_when: disabled` the real
        // flag tracks Core's `enabled`; `known_bad_when: enabled` inverts it.
        var flagOn = _knownBadWhenDisabled ? enabled : !enabled;

        string Substitute(string template) => template
            .Replace("{{featureKey}}", _featureKey)
            .Replace("{{state}}", flagOn ? "enabled" : "disabled")
            .Replace("{{enabled}}", flagOn ? "true" : "false");

        var url = Substitute(_urlTemplate);
        using var request = new HttpRequestMessage(new HttpMethod(_method), url);

        foreach (var (key, value) in _headerTemplates)
        {
            request.Headers.TryAddWithoutValidation(key, Substitute(value));
        }

        if (_bodyTemplate is not null)
        {
            request.Content = new StringContent(Substitute(_bodyTemplate), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FlagControlException($"flag-proof control request {_method} {url} could not be sent: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new FlagControlException(
                    $"flag-proof control request {_method} {url} returned {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }
    }
}
