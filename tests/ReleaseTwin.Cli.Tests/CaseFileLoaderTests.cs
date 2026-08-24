using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Tests;

public class CaseFileLoaderTests
{
    private static string CreateTempWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-cli-tests-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    [Fact]
    public void WellFormedCaseFileLoadsSuccessfully()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{\"amount\":500}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CLM-042
            oracle:
              locator: tickets/CLM-042
            fixture:
              locator: claim.json
            requires:
              - http:azure-devops
            preconditions:
              - check: azdo.areaPathExists
                owner: QA
            pipeline:
              - operation: azdo.createWorkItem
              - operation: azdo.getWorkItem
            cleanup:
              - operation: azdo.deleteWorkItem
            resource_key: TeamProject\Area
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        var cases = loader.LoadAll();

        Assert.Single(cases);
        var testCase = cases[0].Case;
        Assert.Null(cases[0].FlagProof);
        Assert.Equal("CLM-042", testCase.CaseId);
        Assert.Equal("tickets/CLM-042", testCase.Oracle.Locator);
        Assert.Equal(2, testCase.Pipeline.Count);
        Assert.Equal("azdo.createWorkItem", testCase.Pipeline[0].OperationName);
        Assert.Single(testCase.Prerequisites);
        Assert.Equal("QA", testCase.Prerequisites[0].Owner);
        Assert.Single(testCase.Cleanup);
        Assert.Equal("TeamProject\\Area", testCase.ResourceKey!.Value);
        Assert.Single(testCase.RequiredCapabilities);
        Assert.Equal("http:azure-devops", testCase.RequiredCapabilities[0].Name);
    }

    [Fact]
    public void FixtureContentIsLoadedAndHashVerified()
    {
        var root = CreateTempWorkspace();
        var content = "{\"amount\":500}";
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), content);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), $"""
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
              sha256: {expectedHash}
            pipeline: []
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        var testCase = loader.LoadAll().Single().Case;

        Assert.Equal(content, System.Text.Encoding.UTF8.GetString(testCase.Fixture.Content));
        Assert.Equal(expectedHash, testCase.Fixture.ExpectedSha256);
    }

    [Fact]
    public void FixtureLocatorCannotEscapeFixtureRoot()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: ../../etc/passwd
            pipeline: []
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("case1.yaml", ex.Message);
    }

    [Fact]
    public void MissingRequiredFieldIsRejectedBeforeExecution()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "good.yaml"), """
            id: GOOD-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            """);
        File.WriteAllText(Path.Combine(root, "cases", "bad.yaml"), """
            oracle:
              locator: t/2
            fixture:
              locator: claim.json
            pipeline: []
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("bad.yaml", ex.Message);
        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void CaseFileWithFlagProofBlockLoadsTheDeclaration()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            flag_proof:
              feature_key: release-proof-feature
              build_identity: build-123
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        var loaded = loader.LoadAll().Single();

        Assert.NotNull(loaded.FlagProof);
        Assert.Equal("release-proof-feature", loaded.FlagProof!.FeatureKey);
        Assert.Equal("build-123", loaded.FlagProof.BuildIdentity);
    }

    [Fact]
    public void CaseFileWithoutFlagProofBlockLeavesItNull()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        var loaded = loader.LoadAll().Single();

        Assert.Null(loaded.FlagProof);
    }

    [Fact]
    public void MalformedFlagProofBlockIsRejected()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            flag_proof:
              feature_key: release-proof-feature
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("build_identity", ex.Message);
    }

    [Fact]
    public void InvalidYamlIsRejectedWithFileNamed()
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "cases", "broken.yaml"), "id: [this is not: valid: yaml");

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("broken.yaml", ex.Message);
    }
}
