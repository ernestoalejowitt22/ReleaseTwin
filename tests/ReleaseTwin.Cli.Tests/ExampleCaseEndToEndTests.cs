using ReleaseTwin.Cli.CaseLoading;

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

    [Theory]
    [InlineData("example-entra-api-auth.yaml", "ENTRA-API-AUTH-DEMO-1")]
    [InlineData("example-flag-proof-http-entra.yaml", "FLAGPROOF-ENTRA-DEMO-1")]
    public void EntraExampleCasesLoadWithEveryCredentialResolvedFromTheEnvironment(string file, string expectedId)
    {
        // enterprise-network-and-sso: the shipped Entra examples must parse, verify their fixture
        // hash, and hold no literal credential — every ${VAR} resolves from the environment here.
        var casesRoot = FindExamplesDirectory();
        var fixturesRoot = Path.Combine(Directory.GetParent(casesRoot)!.FullName, "fixtures");
        var singleCaseDir = Path.Combine(Path.GetTempPath(), "rt-example-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(singleCaseDir);
        // Entra examples live in examples/cases/enterprise/ — a subdirectory the batch loader
        // (TopDirectoryOnly) does not scan, since they require credentials to actually run.
        File.Copy(Path.Combine(casesRoot, "enterprise", file), Path.Combine(singleCaseDir, file));

        var loader = new CaseFileLoader(singleCaseDir, fixturesRoot, _ => "placeholder-value");
        var loaded = loader.LoadAll().Single();

        Assert.Equal(expectedId, loaded.Case.CaseId);
        if (loaded.FlagProof?.Control is { } control)
        {
            Assert.NotNull(control.Auth);
            Assert.Equal("placeholder-value", control.Auth!.ClientSecret);
        }

        Directory.Delete(singleCaseDir, recursive: true);
    }
}
