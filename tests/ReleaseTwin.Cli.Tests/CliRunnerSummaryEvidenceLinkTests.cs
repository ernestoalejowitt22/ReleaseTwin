using System.Net;
using System.Text;
using System.Text.Json;

namespace ReleaseTwin.Cli.Tests;

/// <summary>pr-annotation-evidence-link: the run summary carries the dashboard URLs the ingest
/// response returns — and only when an upload actually happened.</summary>
public class CliRunnerSummaryEvidenceLinkTests
{
    private const string ReportUrl = "https://app.example.com/dashboard/reports/rid/evidence?projectId=pid";
    private const string RunUrl = "https://app.example.com/dashboard?projectId=pid";

    private sealed class IngestHandler : HttpMessageHandler
    {
        public bool EvidenceAccepted { get; set; } = true;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
        public bool IncludeUrls { get; set; } = true;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = IncludeUrls
                ? $"{{\"id\":\"rid\",\"evidenceAccepted\":{EvidenceAccepted.ToString().ToLowerInvariant()},\"reportUrl\":\"{ReportUrl}\",\"runUrl\":\"{RunUrl}\"}}"
                : $"{{\"id\":\"rid\",\"evidenceAccepted\":{EvidenceAccepted.ToString().ToLowerInvariant()}}}";
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class OkHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", Encoding.UTF8, "application/json"),
            });
    }

    private static (string CasesDir, string SummaryPath) Workspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-summary-link-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "c.yaml"), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
        return (Path.Combine(root, "cases"), Path.Combine(root, "summary.json"));
    }

    private static JsonElement Read(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement;

    [Fact]
    public async Task RunUrlAndEvidenceUrlComeFromTheUploadResponse()
    {
        var (cases, summaryPath) = Workspace();

        var exit = await new CliRunner().RunAsync(cases,
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_SUMMARY_JSON"] = summaryPath,
            },
            new StringWriter(),
            uploadHandlerForTesting: new IngestHandler(),
            httpAdapterHandlerForTesting: new OkHttp());

        Assert.Equal(0, exit);
        var s = Read(summaryPath);
        Assert.Equal(RunUrl, s.GetProperty("runUrl").GetString());
        Assert.Equal(ReportUrl, s.GetProperty("cases")[0].GetProperty("evidenceUrl").GetString());
    }

    [Fact]
    public async Task EvidenceUrlIsOmittedWhenEvidenceWasNotAccepted()
    {
        var (cases, summaryPath) = Workspace();

        await new CliRunner().RunAsync(cases,
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_SUMMARY_JSON"] = summaryPath,
            },
            new StringWriter(),
            uploadHandlerForTesting: new IngestHandler { EvidenceAccepted = false },
            httpAdapterHandlerForTesting: new OkHttp());

        var s = Read(summaryPath);
        Assert.Equal(RunUrl, s.GetProperty("runUrl").GetString());
        Assert.False(s.GetProperty("cases")[0].TryGetProperty("evidenceUrl", out _));
    }

    [Fact]
    public async Task NoUploadLeavesTheSummaryFreeOfUrlKeys()
    {
        var (cases, summaryPath) = Workspace();

        await new CliRunner().RunAsync(cases,
            new Dictionary<string, string?> { ["RELEASETWIN_SUMMARY_JSON"] = summaryPath },
            new StringWriter(),
            httpAdapterHandlerForTesting: new OkHttp());

        var s = Read(summaryPath);
        Assert.Equal(2, s.GetProperty("schemaVersion").GetInt32());
        Assert.False(s.TryGetProperty("runUrl", out _));
        Assert.False(s.GetProperty("cases")[0].TryGetProperty("evidenceUrl", out _));
    }

    [Fact]
    public async Task AnUploadFailureLeavesTheUrlsUnset()
    {
        var (cases, summaryPath) = Workspace();

        var exit = await new CliRunner().RunAsync(cases,
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_SUMMARY_JSON"] = summaryPath,
            },
            new StringWriter(),
            uploadHandlerForTesting: new IngestHandler { Status = HttpStatusCode.InternalServerError },
            httpAdapterHandlerForTesting: new OkHttp());

        Assert.Equal(0, exit); // upload failure is a warning, not a case failure
        var s = Read(summaryPath);
        Assert.False(s.TryGetProperty("runUrl", out _));
    }

    [Fact]
    public async Task AnOlderHostedApiWithoutUrlsIsHandled()
    {
        var (cases, summaryPath) = Workspace();

        await new CliRunner().RunAsync(cases,
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_SUMMARY_JSON"] = summaryPath,
            },
            new StringWriter(),
            uploadHandlerForTesting: new IngestHandler { IncludeUrls = false },
            httpAdapterHandlerForTesting: new OkHttp());

        var s = Read(summaryPath);
        Assert.False(s.TryGetProperty("runUrl", out _));
        Assert.False(s.GetProperty("cases")[0].TryGetProperty("evidenceUrl", out _));
    }
}
