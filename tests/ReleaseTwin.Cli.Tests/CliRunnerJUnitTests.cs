using System.Net;
using System.Text;
using System.Xml.Linq;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-report-formats: the JUnit report written from a real run — per-case states, no bodies/secrets, nothing without the flag.</summary>
public class CliRunnerJUnitTests
{
    private static Dictionary<string, string?> AzdoEnv(string? junitPath = null)
    {
        var env = new Dictionary<string, string?>
        {
            ["AZDO_ORG"] = "test-org",
            ["AZDO_PROJECT"] = "TeamProject",
            ["AZDO_PAT"] = "test-pat",
            ["AZDO_AREA_PATH"] = "TeamProject\\Area",
            ["AZDO_VARIABLE_GROUP_ID"] = "1",
        };
        if (junitPath is not null)
        {
            env["RELEASETWIN_JUNIT_XML"] = junitPath;
        }

        return env;
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-junit-run-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WritePlainCase(string root, string caseId, string operation)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            preconditions:
              - check: azdo.areaPathExists
                owner: QA
            pipeline:
              - operation: {operation}
            """);
    }

    private static void WriteFlagProofCase(string root, string caseId, string operation)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            pipeline:
              - operation: {operation}
            flag_proof:
              feature_key: release-proof-feature
              build_identity: build-123
            """);
    }

    [Fact]
    public async Task ReportReflectsPerCaseStatesFromARealRun()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-PASS", "azdo.readFeatureVariable"); // discriminates -> Passed -> pass
        WriteFlagProofCase(root, "FP-BOTHFAIL", "azdo.getWorkItem");     // never passes -> BothFailed -> failure
        WritePlainCase(root, "PLAIN-PASS", "azdo.createWorkItem");
        WritePlainCase(root, "PLAIN-FAIL", "azdo.getWorkItem");          // no prior create -> failure
        var junitPath = Path.Combine(root, "junit.xml");

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), AzdoEnv(junitPath), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        var doc = XDocument.Load(junitPath);
        var cases = doc.Descendants("testcase").ToDictionary(e => e.Attribute("name")!.Value);

        Assert.Empty(cases["FP-PASS"].Elements());
        Assert.Equal("BothFailed", cases["FP-BOTHFAIL"].Element("failure")!.Attribute("message")!.Value);
        Assert.Empty(cases["PLAIN-PASS"].Elements());
        Assert.NotNull(cases["PLAIN-FAIL"].Element("failure"));

        Assert.Empty(doc.Descendants("skipped"));
        Assert.Equal("4", doc.Root!.Attribute("tests")!.Value);
        Assert.Equal("2", doc.Root!.Attribute("failures")!.Value);
    }

    [Fact]
    public async Task IneligibleFlagProofCaseIsAFailureInTheReport()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-INELIGIBLE", "azdo.readFeatureVariable");
        var junitPath = Path.Combine(root, "junit.xml");

        // No adapter credentials at all -> the flag-proof case is Ineligible.
        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_JUNIT_XML"] = junitPath },
            new StringWriter());

        var testcase = XDocument.Load(junitPath).Descendants("testcase").Single();
        Assert.Equal("Ineligible", testcase.Element("failure")!.Attribute("message")!.Value);
    }

    [Fact]
    public async Task NoFlagMeansNoReport()
    {
        var root = CreateWorkspace();
        WritePlainCase(root, "PLAIN-PASS", "azdo.createWorkItem");

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), AzdoEnv(), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.Empty(Directory.GetFiles(root, "*.xml"));
    }

    private sealed class SucceedingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed-secret-body\"}", Encoding.UTF8, "application/json"),
            });
    }

    private sealed class AcceptingIngestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"evidenceAccepted\":true}", Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task ReportCarriesNoBodiesOrSecretsEvenWithEvidenceEnabled()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "c.yaml"), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
                  headers:
                    Authorization: Bearer ${SUPER_SECRET}
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed-secret-body
            """);
        var junitPath = Path.Combine(root, "junit.xml");

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["RELEASETWIN_API_TOKEN"] = "tok",
                ["RELEASETWIN_EVIDENCE"] = "on",
                ["SUPER_SECRET"] = "s3cr3t-value",
                ["RELEASETWIN_JUNIT_XML"] = junitPath,
            },
            new StringWriter(),
            uploadHandlerForTesting: new AcceptingIngestHandler(),
            httpAdapterHandlerForTesting: new SucceedingHttpHandler());

        var raw = File.ReadAllText(junitPath);
        Assert.DoesNotContain("s3cr3t-value", raw);
        Assert.DoesNotContain("confirmed-secret-body", raw);
        Assert.DoesNotContain("Bearer", raw);
        // The case id and outcome are still there.
        Assert.Contains("CASE-1", raw);
    }
}
