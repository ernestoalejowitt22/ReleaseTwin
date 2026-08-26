using System.Net;
using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http.Tests;

public class HttpAdapterTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static TestCase BuildCase(IReadOnlyList<PipelineStep> pipeline) => new(
        "CASE-1",
        new OracleReference("tickets/CASE-1"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        Array.Empty<CleanupDeclaration>());

    private static CaseExecutor BuildExecutor(HttpAdapter adapter)
    {
        var root = new CompositionRoot();
        root.Install(adapter);
        return root.BuildExecutor();
    }

    [Fact]
    public void AdapterInstallsWithNoConfiguration()
    {
        using var adapter = new HttpAdapter(new FakeHandler(HttpStatusCode.OK, "{}"));

        Assert.Equal("http", adapter.Name);
    }

    [Fact]
    public async Task RequestParametersDriveTheActualHttpCall()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"status\":\"confirmed\"}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.request", With: new Dictionary<string, object?>
            {
                ["method"] = "POST",
                ["url"] = "https://example.com/orders",
                ["body"] = new Dictionary<string, object?> { ["productId"] = 123 },
            }),
        }));

        Assert.True(report.Passed);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://example.com/orders", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task MatchingJsonPathAssertionPasses()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"status\":\"confirmed\",\"amount\":500}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.request", With: new Dictionary<string, object?> { ["url"] = "https://example.com/orders/1" }),
            new PipelineStep("http.assertJsonPath", With: new Dictionary<string, object?> { ["path"] = "$.status", ["expected"] = "confirmed" }),
        }));

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task MismatchedJsonPathAssertionFailsWithDetail()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"status\":\"pending\"}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.request", With: new Dictionary<string, object?> { ["url"] = "https://example.com/orders/1" }),
            new PipelineStep("http.assertJsonPath", With: new Dictionary<string, object?> { ["path"] = "$.status", ["expected"] = "confirmed" }),
        }));

        Assert.False(report.Passed);
        Assert.Contains("confirmed", report.FailureDetail);
        Assert.Contains("pending", report.FailureDetail);
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        public SequencedHandler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    [Fact]
    public async Task CapturedJsonFieldIsUsableAsABearerHeaderInALaterStep()
    {
        var login = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"token\":\"abc123\"}", Encoding.UTF8, "application/json"),
        };
        var me = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"user\":\"ernesto\"}", Encoding.UTF8, "application/json"),
        };
        var handler = new SequencedHandler(login, me);
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep(
                "http.request",
                With: new Dictionary<string, object?> { ["url"] = "https://example.com/v1/e2e/login", ["method"] = "POST" },
                Capture: new[] { new CaptureDeclaration("token", "json:$.token") }),
            new PipelineStep(
                "http.request",
                With: new Dictionary<string, object?>
                {
                    ["url"] = "https://example.com/api/me",
                    ["headers"] = new Dictionary<string, object?> { ["Authorization"] = "Bearer {{token}}" },
                }),
        }));

        Assert.True(report.Passed);
        Assert.Equal("Bearer abc123", handler.Requests[1].Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task ReferencingAnUndeclaredCaptureFailsTheCaseWithoutSendingTheRequest()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep(
                "http.request",
                With: new Dictionary<string, object?>
                {
                    ["url"] = "https://example.com/api/me",
                    ["headers"] = new Dictionary<string, object?> { ["Authorization"] = "Bearer {{token}}" },
                }),
        }));

        Assert.False(report.Passed);
        Assert.Equal("missing-capture:token", report.FailureDetail);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task UsernameAndPasswordBuildABasicAuthHeaderAutomatically()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.request", With: new Dictionary<string, object?>
            {
                ["url"] = "https://example.com/secure",
                ["username"] = "alice",
                ["password"] = "s3cret",
            }),
        }));

        Assert.True(report.Passed);
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret"));
        Assert.Equal(expected, handler.LastRequest!.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task ExplicitAuthorizationHeaderTakesPrecedenceOverUsernamePassword()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.request", With: new Dictionary<string, object?>
            {
                ["url"] = "https://example.com/secure",
                ["username"] = "alice",
                ["password"] = "s3cret",
                ["headers"] = new Dictionary<string, object?> { ["Authorization"] = "Bearer explicit-token" },
            }),
        }));

        Assert.True(report.Passed);
        Assert.Equal("Bearer explicit-token", handler.LastRequest!.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task Oauth2ClientCredentialsCapturesTheAccessTokenLikeAnyOtherCapture()
    {
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"tok-abc\",\"expires_in\":3600}", Encoding.UTF8, "application/json"),
        };
        var protectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
        };
        var handler = new SequencedHandler(tokenResponse, protectedResponse);
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep(
                "http.oauth2ClientCredentials",
                With: new Dictionary<string, object?>
                {
                    ["tokenUrl"] = "https://example.com/oauth/token",
                    ["clientId"] = "client-1",
                    ["clientSecret"] = "secret-1",
                },
                Capture: new[] { new CaptureDeclaration("token", "json:$.access_token") }),
            new PipelineStep(
                "http.request",
                With: new Dictionary<string, object?>
                {
                    ["url"] = "https://example.com/protected",
                    ["headers"] = new Dictionary<string, object?> { ["Authorization"] = "Bearer {{token}}" },
                }),
        }));

        Assert.True(report.Passed);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("grant_type=client_credentials", handler.RequestBodies[0]);
        Assert.Contains("client_id=client-1", handler.RequestBodies[0]);
        Assert.Equal("Bearer tok-abc", handler.Requests[1].Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task Oauth2ClientCredentialsFailsClearlyWhenMissingRequiredParameters()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        using var adapter = new HttpAdapter(handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase(new[]
        {
            new PipelineStep("http.oauth2ClientCredentials", With: new Dictionary<string, object?> { ["tokenUrl"] = "https://example.com/oauth/token" }),
        }));

        Assert.False(report.Passed);
        Assert.Contains("clientId", report.FailureDetail);
    }
}
