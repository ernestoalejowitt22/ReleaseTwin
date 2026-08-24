namespace ReleaseTwin.Cli.Tests;

public class CliRunnerTests
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
        var root = Directory.CreateTempSubdirectory("releasetwin-cli-runner-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteCase(string root, string fileName, string caseId, string operation)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", fileName), $"""
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

    [Fact]
    public async Task MissingRequiredEnvironmentVariableIsAClearStartupError()
    {
        var root = CreateWorkspace();
        var env = ValidEnvironment();
        env.Remove("AZDO_PAT");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(Path.Combine(root, "cases"), env, output);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("AZDO_PAT", output.ToString());
    }

    [Fact]
    public async Task AllPassingRunProducesZeroExitCode()
    {
        var root = CreateWorkspace();
        WriteCase(root, "case1.yaml", "CASE-1", "azdo.createWorkItem");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ValidEnvironment(), output, azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS CASE-1", output.ToString());
        Assert.Contains("1 passed, 0 failed", output.ToString());
    }

    [Fact]
    public async Task MixedPassFailRunReportsBothAndNonZeroExitCode()
    {
        var root = CreateWorkspace();
        WriteCase(root, "case1.yaml", "CASE-PASS", "azdo.createWorkItem");
        WriteCase(root, "case2.yaml", "CASE-FAIL", "azdo.getWorkItem"); // no prior create -> fails
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ValidEnvironment(), output, azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.NotEqual(0, exitCode);
        var text = output.ToString();
        Assert.Contains("PASS CASE-PASS", text);
        Assert.Contains("FAIL CASE-FAIL", text);
        Assert.Contains("1 passed, 1 failed", text);
    }
}
