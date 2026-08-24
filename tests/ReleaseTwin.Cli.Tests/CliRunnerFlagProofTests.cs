using System.Net;

namespace ReleaseTwin.Cli.Tests;

public class CliRunnerFlagProofTests
{
    private static Dictionary<string, string?> ValidEnvironment() => new()
    {
        ["AZDO_ORG"] = "test-org",
        ["AZDO_PROJECT"] = "TeamProject",
        ["AZDO_PAT"] = "test-pat",
        ["AZDO_AREA_PATH"] = "TeamProject\\Area",
        ["AZDO_VARIABLE_GROUP_ID"] = "1",
    };

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-flag-proof-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteFlagProofCase(string root, string caseId)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            pipeline:
              - operation: azdo.readFeatureVariable
            flag_proof:
              feature_key: release-proof-feature
              build_identity: build-123
            """);
    }

    // Scenario: Case file declares flag-proof mode — azdo.readFeatureVariable fails when the
    // variable-group value is toggled off (known-bad) and passes when toggled on (known-good), so
    // this is the discriminating Passed outcome.
    [Fact]
    public async Task FlagProofCaseReportsThePassedOutcome()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-1");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ValidEnvironment(), output, azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        var text = output.ToString();
        Assert.Contains("FLAGPROOF FP-1 (Passed)", text);
        Assert.Equal(0, exitCode);
    }

    // Scenario: Both legs failing is reported distinctly (an outcome other than Passed)
    [Fact]
    public async Task FlagProofCaseWithAnAlwaysFailingOracleReportsBothFailed()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "FP-2.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "FP-2.yaml"), """
            id: FP-2
            oracle:
              locator: t/FP-2
            fixture:
              locator: FP-2.json
            pipeline:
              - operation: azdo.getWorkItem
            flag_proof:
              feature_key: release-proof-feature
              build_identity: build-123
            """);
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ValidEnvironment(), output, azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        var text = output.ToString();
        Assert.Contains("FLAGPROOF FP-2 (BothFailed)", text);
        Assert.NotEqual(0, exitCode);
    }

    // Scenario: No installed adapter supports feature-state control
    [Fact]
    public async Task FlagProofCaseWithNoAzureDevOpsConfiguredIsIneligible()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-1");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), new Dictionary<string, string?>(), output,
            httpAdapterHandlerForTesting: new NeverCalledHandler());

        var text = output.ToString();
        Assert.Contains("FLAGPROOF FP-1 (Ineligible)", text);
        Assert.NotEqual(0, exitCode);
    }

    // Scenario: mixed run combines an ordinary case and a flag-proof case correctly
    [Fact]
    public async Task MixedOrdinaryAndFlagProofRunCombinesCountsAndExitCode()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "ORD-1.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "ordinary.yaml"), """
            id: ORD-1
            oracle:
              locator: t/ORD-1
            fixture:
              locator: ORD-1.json
            pipeline:
              - operation: azdo.getWorkItem
            """);
        WriteFlagProofCase(root, "FP-1");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ValidEnvironment(), output, azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        var text = output.ToString();
        Assert.Contains("FAIL ORD-1", text);
        Assert.Contains("FLAGPROOF FP-1 (Passed)", text);
        Assert.Contains("1 passed, 1 failed", text);
        Assert.NotEqual(0, exitCode);
    }

    // Scenario: flag-proof result is uploaded for flag-proof cases
    [Fact]
    public async Task FlagProofResultIsUploadedInsteadOfACaseReport()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-1");
        var uploadHandler = new RecordingUploadHandler();
        var output = new StringWriter();

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>(ValidEnvironment()) { ["RELEASETWIN_API_TOKEN"] = "rtw_test" },
            output,
            azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler(),
            uploadHandlerForTesting: uploadHandler);

        Assert.Equal(1, uploadHandler.Invocations);
        Assert.Contains("flag-proof-report", uploadHandler.LastRequest!.RequestUri!.ToString());
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no HTTP request expected in this test");
    }

    private sealed class RecordingUploadHandler : HttpMessageHandler
    {
        public int Invocations { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
