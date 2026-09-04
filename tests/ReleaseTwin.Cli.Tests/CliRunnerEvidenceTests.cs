using System.Net;
using System.Text;

namespace ReleaseTwin.Cli.Tests;

public class CliRunnerEvidenceTests
{
    private sealed class CapturingIngestHandler : HttpMessageHandler
    {
        public int Invocations { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastContentType { get; private set; }
        public bool EvidenceAccepted { get; set; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent($"{{\"evidenceAccepted\":{EvidenceAccepted.ToString().ToLowerInvariant()}}}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SucceedingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", Encoding.UTF8, "application/json"),
            });
    }

    private static string CreateWorkspaceWithHttpCase()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-cli-evidence-").FullName;
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
        return root;
    }

    [Fact]
    public async Task NoOptIn_UploadsReportWithoutEvidence()
    {
        var root = CreateWorkspaceWithHttpCase();
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "tok" },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Equal(1, ingest.Invocations);
        Assert.DoesNotContain("evidence", ingest.LastBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnvOptIn_UploadsEvidenceDocument()
    {
        var root = CreateWorkspaceWithHttpCase();
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
            },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Contains("\"evidence\"", ingest.LastBody!);
        Assert.Contains("http.assertJsonPath", ingest.LastBody!);
        Assert.Contains("Redacted by your CLI before upload", ingest.LastBody!);
    }

    [Fact]
    public async Task CaptureWithoutToken_DoesNotUpload()
    {
        var root = CreateWorkspaceWithHttpCase();
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_EVIDENCE"] = "on" },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Equal(0, ingest.Invocations);
    }

    [Fact]
    public async Task EvidenceRejected_IsWarningOnly()
    {
        var root = CreateWorkspaceWithHttpCase();
        var ingest = new CapturingIngestHandler { EvidenceAccepted = false };
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
            },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Equal(1, ingest.Invocations);
        var text = output.ToString();
        Assert.Contains("PASS CASE-1", text);
        Assert.Contains("evidence not accepted", text);
    }

    // local-evidence-artifacts: RELEASETWIN_EVIDENCE_DIR writes redacted evidence to disk with no
    // hosted token required at all.
    [Fact]
    public async Task LocalDirWithoutToken_WritesEvidenceAndDoesNotUpload()
    {
        var root = CreateWorkspaceWithHttpCase();
        var evidenceDir = Directory.CreateTempSubdirectory("releasetwin-cli-evidence-dir-").FullName;
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_EVIDENCE_DIR"] = evidenceDir,
            },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Equal(0, ingest.Invocations);
        var documentPath = Path.Combine(evidenceDir, "CASE-1", "evidence.json");
        Assert.True(File.Exists(documentPath));
        var json = File.ReadAllText(documentPath);
        Assert.Contains("http.assertJsonPath", json);
        Assert.Contains("Redacted by your CLI before upload", json);
    }

    [Fact]
    public async Task LocalDirAndToken_BothWriteAndUpload()
    {
        var root = CreateWorkspaceWithHttpCase();
        var evidenceDir = Directory.CreateTempSubdirectory("releasetwin-cli-evidence-dir-").FullName;
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_EVIDENCE_DIR"] = evidenceDir,
            },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.Equal(1, ingest.Invocations);
        Assert.Contains("\"evidence\"", ingest.LastBody!);
        Assert.True(File.Exists(Path.Combine(evidenceDir, "CASE-1", "evidence.json")));
    }

    [Fact]
    public async Task CaptureWithNeitherDestination_ProducesNoOutputAndNoWarning()
    {
        var root = CreateWorkspaceWithHttpCase();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_EVIDENCE"] = "on" },
            output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        var text = output.ToString();
        Assert.Contains("PASS CASE-1", text);
        Assert.DoesNotContain("WARN", text);
    }

    [Fact]
    public async Task LocalWriteFailure_IsWarningOnlyAndUploadStillProceeds()
    {
        var root = CreateWorkspaceWithHttpCase();
        var evidenceParent = Directory.CreateTempSubdirectory("releasetwin-cli-evidence-dir-").FullName;
        // A file where the case's evidence subdirectory would go makes Directory.CreateDirectory throw.
        File.WriteAllText(Path.Combine(evidenceParent, "CASE-1"), "not a directory");
        var ingest = new CapturingIngestHandler();
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_EVIDENCE_DIR"] = evidenceParent,
            },
            output,
            uploadHandlerForTesting: ingest,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        var text = output.ToString();
        Assert.Contains("PASS CASE-1", text);
        Assert.Contains("WARN local evidence write failed for CASE-1", text);
        Assert.Equal(1, ingest.Invocations);
        Assert.Contains("\"evidence\"", ingest.LastBody!);
    }

    [Fact]
    public async Task TwoCases_WriteToSeparateSubdirectories()
    {
        var root = CreateWorkspaceWithHttpCase();
        File.WriteAllText(Path.Combine(root, "fixtures", "f2.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "c2.yaml"), """
            id: CASE-2
            oracle: { locator: t/2 }
            fixture: { locator: f2.json }
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
        var evidenceDir = Directory.CreateTempSubdirectory("releasetwin-cli-evidence-dir-").FullName;
        var output = new StringWriter();

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["RELEASETWIN_EVIDENCE_DIR"] = evidenceDir,
            },
            output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(evidenceDir, "CASE-1", "evidence.json")));
        Assert.True(File.Exists(Path.Combine(evidenceDir, "CASE-2", "evidence.json")));
    }
}
