using System.Net;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http.Tests;

public class HttpFeatureStateControllerTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public List<(string Method, string Url, string? Body)> Calls { get; } = new();

        public RecordingHandler(HttpStatusCode status) => _status = status;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(_status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private static HttpFeatureStateController Build(HttpClient client, bool knownBadWhenDisabled) => new(
        client,
        featureKey: "checkout-v2",
        method: "PUT",
        urlTemplate: "https://flags.example/flags/{{featureKey}}",
        headerTemplates: new Dictionary<string, string> { ["Authorization"] = "Bearer t0ken" },
        bodyTemplate: "{ \"state\": \"{{state}}\", \"on\": {{enabled}} }",
        knownBadWhenDisabled: knownBadWhenDisabled);

    [Fact]
    public async Task DefaultPolaritySendsDisabledThenEnabled()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: true);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        Assert.Equal("{ \"state\": \"disabled\", \"on\": false }", handler.Calls[0].Body);
        Assert.Equal("{ \"state\": \"enabled\", \"on\": true }", handler.Calls[1].Body);
        Assert.Equal("https://flags.example/flags/checkout-v2", handler.Calls[0].Url);
        Assert.Equal("PUT", handler.Calls[0].Method);
    }

    [Fact]
    public async Task KnownBadWhenEnabledInvertsPolarity()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: false);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        Assert.Equal("{ \"state\": \"enabled\", \"on\": true }", handler.Calls[0].Body);
        Assert.Equal("{ \"state\": \"disabled\", \"on\": false }", handler.Calls[1].Body);
    }

    [Fact]
    public async Task NonSuccessStatusThrowsFlagControlException()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: true);

        var ex = await Assert.ThrowsAsync<FlagControlException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("500", ex.Message);
        Assert.Contains("https://flags.example/flags/checkout-v2", ex.Message);
    }

    /// <summary>Answers the control request (non-GET) with 200, and the verify request (GET) with a
    /// scripted status + body so a read-back can be exercised.</summary>
    private sealed class ControlThenVerifyHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _verifyStatus;
        private readonly string _verifyBody;
        public List<(string Method, string Url, string? Auth)> Calls { get; } = new();

        public ControlThenVerifyHandler(HttpStatusCode verifyStatus, string verifyBody)
        {
            _verifyStatus = verifyStatus;
            _verifyBody = verifyBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(",", values) : null;
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), auth));

            var isVerify = request.Method == HttpMethod.Get;
            return Task.FromResult(isVerify
                ? new HttpResponseMessage(_verifyStatus) { Content = new StringContent(_verifyBody, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    private static HttpFeatureStateController BuildWithVerify(
        HttpClient client, IReadOnlyDictionary<string, string>? verifyHeaders, string expected = "{{enabled}}", bool knownBadWhenDisabled = true) => new(
        client,
        featureKey: "checkout-v2",
        method: "PUT",
        urlTemplate: "https://flags.example/flags/{{featureKey}}",
        headerTemplates: new Dictionary<string, string> { ["Authorization"] = "Bearer t0ken" },
        bodyTemplate: "{ \"on\": {{enabled}} }",
        knownBadWhenDisabled: knownBadWhenDisabled,
        verify: new HttpFlagVerify("GET", "https://flags.example/flags/{{featureKey}}", verifyHeaders, null, "$.enabled", expected));

    [Fact]
    public async Task MatchingReadBackReturnsNormally()
    {
        var handler = new ControlThenVerifyHandler(HttpStatusCode.OK, "{ \"enabled\": false }");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);

        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal("GET", handler.Calls[1].Method);
    }

    [Fact]
    public async Task MismatchedReadBackThrowsFlagStateUnverified()
    {
        var handler = new ControlThenVerifyHandler(HttpStatusCode.OK, "{ \"enabled\": true }");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null);

        var ex = await Assert.ThrowsAsync<FlagStateUnverifiedException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("expected 'false'", ex.Message);
        Assert.Contains("$.enabled", ex.Message);
    }

    [Fact]
    public async Task ReadBackEndpointFailureThrowsFlagControlException()
    {
        var handler = new ControlThenVerifyHandler(HttpStatusCode.ServiceUnavailable, "");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null);

        var ex = await Assert.ThrowsAsync<FlagControlException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task VerifyHeadersFallBackToControlHeaders()
    {
        var handler = new ControlThenVerifyHandler(HttpStatusCode.OK, "{ \"enabled\": true }");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null);

        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        Assert.Equal("Bearer t0ken", handler.Calls[1].Auth);
    }

    [Fact]
    public async Task EnabledTokenInExpectedMatchesJsonBoolean()
    {
        var handler = new ControlThenVerifyHandler(HttpStatusCode.OK, "{ \"enabled\": true }");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null, expected: "{{enabled}}");

        // known-good leg -> flag on -> {{enabled}} resolves to "true", which must match the JSON
        // boolean `true` in the read-back body (normalised, not "True"). No throw == matched.
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task InvertedPolarityAssertsPerLegValue()
    {
        // known_bad_when: enabled -> known-bad leg (enabled:false) drives the flag ON.
        var handler = new ControlThenVerifyHandler(HttpStatusCode.OK, "{ \"enabled\": true }");
        using var client = new HttpClient(handler);
        var controller = BuildWithVerify(client, verifyHeaders: null, expected: "{{enabled}}", knownBadWhenDisabled: false);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        Assert.Equal(2, handler.Calls.Count);
    }

    /// <summary>Answers a token endpoint (POST to a URL containing "/token") with a scripted status +
    /// body, and every other request with 200. Records each call's form fields or Authorization.</summary>
    private sealed class AuthThenControlHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _tokenStatus;
        private readonly string _tokenBody;
        public List<(string Method, string Url, string? Auth, string? Body)> Calls { get; } = new();

        public AuthThenControlHandler(HttpStatusCode tokenStatus, string tokenBody)
        {
            _tokenStatus = tokenStatus;
            _tokenBody = tokenBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(",", values) : null;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), auth, body));

            var isToken = request.RequestUri!.AbsolutePath.Contains("/token");
            return isToken
                ? new HttpResponseMessage(_tokenStatus) { Content = new StringContent(_tokenBody, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private static HttpFeatureStateController BuildWithAuth(HttpClient client, string? scope = "api://flags/.default") => new(
        client,
        featureKey: "checkout-v2",
        method: "PUT",
        urlTemplate: "https://flags.example/flags/{{featureKey}}",
        headerTemplates: new Dictionary<string, string> { ["Authorization"] = "Bearer {{token}}" },
        bodyTemplate: "{ \"state\": \"{{state}}\" }",
        knownBadWhenDisabled: true,
        auth: new HttpFlagAuth(
            "https://login.example/tenant/oauth2/v2.0/token",
            "client-abc",
            "s3cr3t",
            scope));

    [Fact]
    public async Task AuthMintsTokenPerLegAndSubstitutesIntoControlRequest()
    {
        var handler = new AuthThenControlHandler(HttpStatusCode.OK, "{ \"access_token\": \"minted-xyz\" }");
        using var client = new HttpClient(handler);
        var controller = BuildWithAuth(client);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        // token endpoint hit before each leg's control request: token, control, token, control
        Assert.Equal(4, handler.Calls.Count);
        Assert.Contains("/token", handler.Calls[0].Url);
        Assert.Contains("grant_type=client_credentials", handler.Calls[0].Body);
        Assert.Contains("client_id=client-abc", handler.Calls[0].Body);
        Assert.Contains("scope=api", handler.Calls[0].Body);
        Assert.Equal("PUT", handler.Calls[1].Method);
        Assert.Equal("Bearer minted-xyz", handler.Calls[1].Auth);
        Assert.Equal("Bearer minted-xyz", handler.Calls[3].Auth);
    }

    [Fact]
    public async Task AuthOmitsScopeFromFormWhenNotDeclared()
    {
        var handler = new AuthThenControlHandler(HttpStatusCode.OK, "{ \"access_token\": \"minted-xyz\" }");
        using var client = new HttpClient(handler);
        var controller = BuildWithAuth(client, scope: null);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);

        Assert.DoesNotContain("scope=", handler.Calls[0].Body);
    }

    [Fact]
    public async Task AuthTokenEndpointFailureThrowsFlagControlExceptionWithoutLeakingSecret()
    {
        var handler = new AuthThenControlHandler(HttpStatusCode.Unauthorized, "{ \"error\": \"invalid_client\", \"secret_echo\": \"s3cr3t\" }");
        using var client = new HttpClient(handler);
        var controller = BuildWithAuth(client);

        var ex = await Assert.ThrowsAsync<FlagControlException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain("s3cr3t", ex.Message);
        Assert.Single(handler.Calls); // control request never sent
    }

    [Fact]
    public async Task AuthResponseWithoutAccessTokenThrowsFlagControlException()
    {
        var handler = new AuthThenControlHandler(HttpStatusCode.OK, "{ \"token_type\": \"Bearer\" }");
        using var client = new HttpClient(handler);
        var controller = BuildWithAuth(client);

        var ex = await Assert.ThrowsAsync<FlagControlException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("access_token", ex.Message);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task NoAuthSectionSendsNoTokenRequest()
    {
        var handler = new AuthThenControlHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: true);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);

        Assert.Single(handler.Calls);
        Assert.Equal("PUT", handler.Calls[0].Method);
    }
}
