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
    public void FlagProofControlBlockRoundTripsAndInterpolatesEnvButNotTemplateTokens()
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
              feature_key: checkout-v2
              build_identity: build-123
              control:
                method: put
                url: ${FLAGS_API}/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer ${FLAGS_TOKEN}"
                body: '{"state":"{{state}}","on":{{enabled}}}'
                known_bad_when: enabled
            """);

        var loader = new CaseFileLoader(
            Path.Combine(root, "cases"), Path.Combine(root, "fixtures"),
            name => name switch { "FLAGS_API" => "https://flags.example", "FLAGS_TOKEN" => "s3cret", _ => null });
        var control = loader.LoadAll().Single().FlagProof!.Control!;

        Assert.Equal("PUT", control.Method);
        Assert.Equal("https://flags.example/flags/{{featureKey}}", control.Url);
        Assert.Equal("Bearer s3cret", control.Headers["Authorization"]);
        Assert.Equal("{\"state\":\"{{state}}\",\"on\":{{enabled}}}", control.Body);
        Assert.Equal(FlagProofPolarity.KnownBadWhenEnabled, control.Polarity);
    }

    [Fact]
    public void FlagProofControlWithoutUrlIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
            """);
        Assert.Contains("url", ex.Message);
    }

    [Fact]
    public void FlagProofControlWithBadMethodIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: FETCH
                url: https://flags.example/f
            """);
        Assert.Contains("method", ex.Message);
    }

    [Fact]
    public void FlagProofControlWithBadKnownBadWhenIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                known_bad_when: sometimes
            """);
        Assert.Contains("known_bad_when", ex.Message);
    }

    private CaseFileException LoadControlCase(string controlYaml)
    {
        var root = CreateTempWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), $"""
            id: CLM-1
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            flag_proof:
              feature_key: checkout-v2
              build_identity: build-123
            {controlYaml}
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        return Assert.Throws<CaseFileException>(() => loader.LoadAll());
    }

    [Fact]
    public void FlagProofControlVerifyBlockRoundTripsAndInterpolatesEnvButNotTemplateTokens()
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
              feature_key: checkout-v2
              build_identity: build-123
              control:
                method: put
                url: ${FLAGS_API}/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer ${FLAGS_TOKEN}"
                verify:
                  url: ${FLAGS_API}/flags/{{featureKey}}
                  json_path: $.enabled
                  expected: "{{enabled}}"
            """);

        var loader = new CaseFileLoader(
            Path.Combine(root, "cases"), Path.Combine(root, "fixtures"),
            name => name switch { "FLAGS_API" => "https://flags.example", "FLAGS_TOKEN" => "s3cret", _ => null });
        var verify = loader.LoadAll().Single().FlagProof!.Control!.Verify!;

        Assert.Equal("GET", verify.Method);
        Assert.Equal("https://flags.example/flags/{{featureKey}}", verify.Url);
        Assert.Equal("$.enabled", verify.JsonPath);
        Assert.Equal("{{enabled}}", verify.Expected);
        Assert.Null(verify.Headers);
    }

    [Fact]
    public void FlagProofControlVerifyWithoutUrlIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                verify:
                  json_path: $.enabled
                  expected: "true"
            """);
        Assert.Contains("verify", ex.Message);
        Assert.Contains("url", ex.Message);
    }

    [Fact]
    public void FlagProofControlVerifyWithoutJsonPathIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                verify:
                  url: https://flags.example/f
                  expected: "true"
            """);
        Assert.Contains("json_path", ex.Message);
    }

    [Fact]
    public void FlagProofControlVerifyWithoutExpectedIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                verify:
                  url: https://flags.example/f
                  json_path: $.enabled
            """);
        Assert.Contains("expected", ex.Message);
    }

    [Fact]
    public void FlagProofControlVerifyWithBadMethodIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                verify:
                  method: FETCH
                  url: https://flags.example/f
                  json_path: $.enabled
                  expected: "true"
            """);
        Assert.Contains("verify", ex.Message);
        Assert.Contains("method", ex.Message);
    }

    [Fact]
    public void FlagProofControlAuthBlockRoundTripsAndInterpolatesEnvButNotTemplateTokens()
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
              feature_key: checkout-v2
              build_identity: build-123
              control:
                method: put
                url: ${FLAGS_API}/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer {{token}}"
                auth:
                  oauth2_client_credentials:
                    token_url: ${TENANT}/oauth2/v2.0/token
                    client_id: ${FLAGS_CLIENT_ID}
                    client_secret: ${FLAGS_CLIENT_SECRET}
                    scope: api://flags/.default
            """);

        var loader = new CaseFileLoader(
            Path.Combine(root, "cases"), Path.Combine(root, "fixtures"),
            name => name switch
            {
                "FLAGS_API" => "https://flags.example",
                "TENANT" => "https://login.example/t1",
                "FLAGS_CLIENT_ID" => "client-abc",
                "FLAGS_CLIENT_SECRET" => "s3cr3t",
                _ => null,
            });
        var auth = loader.LoadAll().Single().FlagProof!.Control!.Auth!;

        Assert.Equal("https://login.example/t1/oauth2/v2.0/token", auth.TokenUrl);
        Assert.Equal("client-abc", auth.ClientId);
        Assert.Equal("s3cr3t", auth.ClientSecret);
        Assert.Equal("api://flags/.default", auth.Scope);
    }

    [Fact]
    public void FlagProofControlAuthWithoutClientSecretIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                auth:
                  oauth2_client_credentials:
                    token_url: https://login.example/token
                    client_id: client-abc
            """);
        Assert.Contains("client_secret", ex.Message);
    }

    [Fact]
    public void FlagProofControlAuthWithoutOauth2BlockIsRejected()
    {
        var ex = LoadControlCase("""
              control:
                method: PUT
                url: https://flags.example/f
                auth:
                  something_else: true
            """);
        Assert.Contains("oauth2_client_credentials", ex.Message);
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
