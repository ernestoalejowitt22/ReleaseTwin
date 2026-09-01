using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Raised when a flag-proof control request does not return a 2xx status, or the request cannot be
/// sent at all. The message names the method, URL, and status so the run report can point at the
/// control call as the cause rather than misreporting a leg. A failed read-back request (non-2xx or
/// unsendable) also raises this — the state could not be confirmed.
/// </summary>
public sealed class FlagControlException : Exception
{
    public FlagControlException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// http-flag-control: an optional read-back issued after a control request to confirm the flag
/// actually reached the intended state. <c>${ENV_VAR}</c> is resolved at case-load time;
/// <c>{{featureKey}}</c> / <c>{{state}}</c> / <c>{{enabled}}</c> are substituted per leg — including
/// inside <see cref="Expected"/>.
/// </summary>
public sealed record HttpFlagVerify(
    string Method,
    string UrlTemplate,
    IReadOnlyDictionary<string, string>? HeaderTemplates,
    string? BodyTemplate,
    string JsonPath,
    string ExpectedTemplate);

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
    private readonly HttpFlagVerify? _verify;

    public HttpFeatureStateController(
        HttpClient client,
        string featureKey,
        string method,
        string urlTemplate,
        IReadOnlyDictionary<string, string> headerTemplates,
        string? bodyTemplate,
        bool knownBadWhenDisabled,
        HttpFlagVerify? verify = null)
    {
        _client = client;
        _featureKey = featureKey;
        _method = method;
        _urlTemplate = urlTemplate;
        _headerTemplates = headerTemplates;
        _bodyTemplate = bodyTemplate;
        _knownBadWhenDisabled = knownBadWhenDisabled;
        _verify = verify;
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

        if (_verify is not null)
        {
            await VerifyAsync(_verify, Substitute, cancellationToken);
        }
    }

    private async Task VerifyAsync(HttpFlagVerify verify, Func<string, string> substitute, CancellationToken cancellationToken)
    {
        var url = substitute(verify.UrlTemplate);
        using var request = new HttpRequestMessage(new HttpMethod(verify.Method), url);

        // Fall back to the control block's headers (shared auth) when the verify block declares none.
        var headerTemplates = verify.HeaderTemplates ?? _headerTemplates;
        foreach (var (key, value) in headerTemplates)
        {
            request.Headers.TryAddWithoutValidation(key, substitute(value));
        }

        if (verify.BodyTemplate is not null)
        {
            request.Content = new StringContent(substitute(verify.BodyTemplate), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FlagControlException($"flag-proof verify request {verify.Method} {url} could not be sent: {ex.Message}", ex);
        }

        string body;
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new FlagControlException(
                    $"flag-proof verify request {verify.Method} {url} returned {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        var expected = substitute(verify.ExpectedTemplate);
        var match = JsonPathMatch.Evaluate(body, verify.JsonPath, expected);
        if (match.Error is not null)
        {
            throw new FlagStateUnverifiedException(
                $"flag-proof verify request {verify.Method} {url}: {match.Error}");
        }

        if (!match.Matched)
        {
            throw new FlagStateUnverifiedException(
                $"flag-proof verify request {verify.Method} {url}: expected '{expected}' but got '{match.Actual ?? "<missing>"}' at path '{verify.JsonPath}'");
        }
    }
}
