using System.Net;
using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http.Tests;

public class HttpEvidenceTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        public FakeHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private static TestCase BuildCase(params PipelineStep[] pipeline) => new(
        "CASE-1", new OracleReference("tickets/CASE-1"), ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(), pipeline, Array.Empty<CleanupDeclaration>());

    [Fact]
    public async Task Request_EmitsRequestAndResponseEvidence_WhenCapturing()
    {
        using var adapter = new HttpAdapter(new FakeHandler("{\"status\":\"ok\"}"));
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var testCase = BuildCase(new PipelineStep("http.request", With: new Dictionary<string, object?>
        {
            ["method"] = "GET",
            ["url"] = "https://example.test/thing",
        }));

        var result = await executor.ExecuteAsync(testCase, new ExecutionOptions { CaptureEvidence = true });

        var step = Assert.Single(result.Evidence!.Steps);
        var http = Assert.IsType<HttpRequestEvidence>(step.AdapterEvidence);
        Assert.Equal("GET", http.Method);
        Assert.Equal("https://example.test/thing", http.Url);
        Assert.Equal(200, http.StatusCode);
        Assert.Contains("ok", http.ResponseBody);
    }

    [Fact]
    public async Task Request_EmitsNothing_WhenNotCapturing()
    {
        using var adapter = new HttpAdapter(new FakeHandler("{}"));
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var testCase = BuildCase(new PipelineStep("http.request", With: new Dictionary<string, object?>
        {
            ["url"] = "https://example.test/thing",
        }));

        var result = await executor.ExecuteAsync(testCase, ExecutionOptions.Default);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Request_TruncatesLargeResponseBodyAtCap()
    {
        var big = "{\"x\":\"" + new string('a', HttpEvidence.BodyCapBytes + 5000) + "\"}";
        using var adapter = new HttpAdapter(new FakeHandler(big));
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var testCase = BuildCase(new PipelineStep("http.request", With: new Dictionary<string, object?>
        {
            ["url"] = "https://example.test/thing",
        }));

        var result = await executor.ExecuteAsync(testCase, new ExecutionOptions { CaptureEvidence = true });
        var http = (HttpRequestEvidence)result.Evidence!.Steps[0].AdapterEvidence!;
        Assert.True(http.ResponseBodyTruncated);
        Assert.Equal(HttpEvidence.BodyCapBytes, http.ResponseBody!.Length);
    }

    [Fact]
    public async Task AssertJsonPath_EmitsAssertionDetail()
    {
        using var adapter = new HttpAdapter(new FakeHandler("{\"status\":\"confirmed\"}"));
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var testCase = BuildCase(
            new PipelineStep("http.request", With: new Dictionary<string, object?> { ["url"] = "https://example.test/o" }),
            new PipelineStep("http.assertJsonPath", With: new Dictionary<string, object?>
            {
                ["path"] = "$.status",
                ["expected"] = "confirmed",
            }));

        var result = await executor.ExecuteAsync(testCase, new ExecutionOptions { CaptureEvidence = true });

        var assertStep = result.Evidence!.Steps[1];
        Assert.Equal("$.status", assertStep.Assertion!.Expression);
        Assert.Equal("confirmed", assertStep.Assertion.Expected);
        Assert.Equal("confirmed", assertStep.Assertion.Observed);
    }
}
