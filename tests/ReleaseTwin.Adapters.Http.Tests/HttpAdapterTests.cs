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
}
