namespace ReleaseTwin.Cli.Tests;

/// <summary>tasks.md 4.1: confirms the real example under examples/ (not a throwaway temp fixture) loads and runs through the CLI's own code path.</summary>
public class ExampleCaseEndToEndTests
{
    private static string FindExamplesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "examples", "cases")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Could not locate examples/cases from test output directory.")
            : Path.Combine(dir.FullName, "examples", "cases");
    }

    [Fact]
    public async Task ExampleClaimCaseLoadsAndRunsThroughTheCli()
    {
        var casesDirectory = FindExamplesDirectory();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            casesDirectory,
            new Dictionary<string, string?>
            {
                ["AZDO_ORG"] = "test-org",
                ["AZDO_PROJECT"] = "TeamProject",
                ["AZDO_PAT"] = "test-pat",
                ["AZDO_AREA_PATH"] = "TeamProject\\Area",
                ["AZDO_VARIABLE_GROUP_ID"] = "1",
            },
            output,
            azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS CLM-042", output.ToString());
    }
}
