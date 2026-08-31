using System.Text.Json;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-pr-integration: the run summary written on pass, on fail, with flag-proof + release fields, and never without the flag.</summary>
public class CliRunnerSummaryTests
{
    private static Dictionary<string, string?> Env(string? summaryPath = null)
    {
        var env = new Dictionary<string, string?>
        {
            ["AZDO_ORG"] = "test-org",
            ["AZDO_PROJECT"] = "TeamProject",
            ["AZDO_PAT"] = "test-pat",
            ["AZDO_AREA_PATH"] = "TeamProject\\Area",
            ["AZDO_VARIABLE_GROUP_ID"] = "1",
        };
        if (summaryPath is not null)
        {
            env["RELEASETWIN_SUMMARY_JSON"] = summaryPath;
        }

        return env;
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-summary-run-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteCase(string root, string caseId, string operation, string? release = null)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        var releaseLine = release is null ? "" : $"release: \"{release}\"\n";
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            {releaseLine}oracle:
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

    private static void WriteFlagProofCase(string root, string caseId, string? release = null)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        var releaseLine = release is null ? "" : $"release: \"{release}\"\n";
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            {releaseLine}oracle:
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

    private static JsonElement ReadSummary(string path) =>
        JsonDocument.Parse(File.ReadAllText(path)).RootElement;

    [Fact]
    public async Task SummaryIsWrittenOnAPassingRun()
    {
        var root = CreateWorkspace();
        WriteCase(root, "CASE-1", "azdo.createWorkItem", release: "4.2");
        var summaryPath = Path.Combine(root, "summary.json");

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), Env(summaryPath), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.Equal(0, exit);
        var s = ReadSummary(summaryPath);
        Assert.Equal(1, s.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("passed", s.GetProperty("overall").GetString());
        Assert.Equal(1, s.GetProperty("totals").GetProperty("passed").GetInt32());
        Assert.Equal("4.2", s.GetProperty("cases")[0].GetProperty("release").GetString());
    }

    [Fact]
    public async Task SummaryIsWrittenOnAFailingRunWithOverallFailed()
    {
        var root = CreateWorkspace();
        WriteCase(root, "CASE-PASS", "azdo.createWorkItem");
        WriteCase(root, "CASE-FAIL", "azdo.getWorkItem"); // no prior create -> fails
        var summaryPath = Path.Combine(root, "summary.json");

        var exit = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), Env(summaryPath), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.NotEqual(0, exit);
        var s = ReadSummary(summaryPath);
        Assert.Equal("failed", s.GetProperty("overall").GetString());
        Assert.Equal(1, s.GetProperty("totals").GetProperty("passed").GetInt32());
        Assert.Equal(1, s.GetProperty("totals").GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task FlagProofResultsPopulateThePerCaseFieldAndTheTally()
    {
        var root = CreateWorkspace();
        WriteFlagProofCase(root, "FP-1", release: "4.2");
        var summaryPath = Path.Combine(root, "summary.json");

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), Env(summaryPath), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        var s = ReadSummary(summaryPath);
        Assert.Equal("Passed", s.GetProperty("cases")[0].GetProperty("flagProof").GetString());
        Assert.Equal("4.2", s.GetProperty("cases")[0].GetProperty("release").GetString());
        Assert.Equal(1, s.GetProperty("flagProof").GetProperty("proven").GetInt32());
    }

    [Fact]
    public async Task NoSummaryFlagMeansNoFile()
    {
        var root = CreateWorkspace();
        WriteCase(root, "CASE-1", "azdo.createWorkItem");

        await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), Env(), new StringWriter(), azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.Empty(Directory.GetFiles(root, "*.json"));
    }
}
