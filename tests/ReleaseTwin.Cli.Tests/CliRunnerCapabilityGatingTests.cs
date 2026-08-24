namespace ReleaseTwin.Cli.Tests;

public class CliRunnerCapabilityGatingTests
{
    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-capability-gating-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    // graceful-capability-gating task 3.3: a case referencing azdo.* operations with no `requires:`
    // declared, and no Azure DevOps configured, degrades gracefully instead of crashing.
    [Fact]
    public async Task CaseWithoutRequiresStillDegradesGracefullyWhenAzureDevOpsIsMissing()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "CLM-1.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "CLM-1.yaml"), """
            id: CLM-1
            oracle:
              locator: t/CLM-1
            fixture:
              locator: CLM-1.json
            preconditions:
              - check: azdo.areaPathExists
                owner: QA
            pipeline:
              - operation: azdo.createWorkItem
            cleanup:
              - operation: azdo.deleteWorkItem
            """);
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), new Dictionary<string, string?>(), output);

        var text = output.ToString();
        Assert.Contains("FAIL CLM-1", text);
        Assert.Contains("missing-capability:http:azure-devops", text);
        Assert.NotEqual(0, exitCode);
    }

    // graceful-capability-gating task 3.4: an explicit requires: for a capability no manifest would
    // have inferred still works exactly as before (regression guard for the union).
    [Fact]
    public async Task ExplicitRequiresForAnUninferredCapabilityStillDegradesGracefully()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "BROWSER-1.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "BROWSER-1.yaml"), """
            id: BROWSER-1
            oracle:
              locator: t/BROWSER-1
            fixture:
              locator: BROWSER-1.json
            requires:
              - browser:chromium
            pipeline: []
            """);
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), new Dictionary<string, string?>(), output);

        var text = output.ToString();
        Assert.Contains("FAIL BROWSER-1", text);
        Assert.Contains("missing-capability:browser:chromium", text);
        Assert.NotEqual(0, exitCode);
    }
}
