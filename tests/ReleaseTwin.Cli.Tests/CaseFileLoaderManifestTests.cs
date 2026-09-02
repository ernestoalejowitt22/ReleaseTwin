using ReleaseTwin.Cli.CaseLoading;

namespace ReleaseTwin.Cli.Tests;

/// <summary>flag-proof-project-template: the project manifest (<c>releasetwin.yml</c>) and how a
/// case's inline <c>control</c> block merges over it.</summary>
public class CaseFileLoaderManifestTests
{
    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-manifest-tests-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "claim.json"), "{}");
        return root;
    }

    private static void WriteManifest(string root, string yaml) =>
        File.WriteAllText(Path.Combine(root, "cases", "releasetwin.yml"), yaml);

    private static void WriteCase(string root, string name, string flagProofYaml) =>
        File.WriteAllText(Path.Combine(root, "cases", name), $"""
            id: {Path.GetFileNameWithoutExtension(name)}
            oracle:
              locator: t/1
            fixture:
              locator: claim.json
            pipeline: []
            {flagProofYaml}
            """);

    private static CaseFileLoader Loader(string root, Func<string, string?>? env = null) =>
        new(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"), env);

    [Fact]
    public void CaseWithNoControlInheritsCompleteManifestBlock()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: ${FLAGS_API}/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer ${FLAGS_TOKEN}"
                body: '{ "state": "{{state}}" }'
            """);
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: build-1
            """);

        var control = Loader(root, name => name switch
        {
            "FLAGS_API" => "https://flags.example",
            "FLAGS_TOKEN" => "s3cret",
            _ => null,
        }).LoadAll().Single().FlagProof!.Control!;

        Assert.Equal("PUT", control.Method);
        Assert.Equal("https://flags.example/flags/{{featureKey}}", control.Url);
        Assert.Equal("Bearer s3cret", control.Headers["Authorization"]);
        Assert.Equal("{ \"state\": \"{{state}}\" }", control.Body);
    }

    [Fact]
    public void OneManifestTemplateServesCasesWithDifferentFlagKeys()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: https://flags.example/flags/{{featureKey}}
            """);
        WriteCase(root, "a.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);
        WriteCase(root, "b.yaml", """
            flag_proof:
              feature_key: search-ranking
              build_identity: b2
            """);

        var cases = Loader(root).LoadAll();

        Assert.All(cases, c => Assert.Equal("https://flags.example/flags/{{featureKey}}", c.FlagProof!.Control!.Url));
        Assert.Equal(new[] { "checkout-v2", "search-ranking" }, cases.Select(c => c.FlagProof!.FeatureKey));
    }

    [Fact]
    public void CaseAddsOneHeaderAndVerifyWhileKeepingManifestUrlAuthAndBaseHeaders()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: https://flags.example/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer static"
                auth:
                  oauth2_client_credentials:
                    token_url: https://id.example/token
                    client_id: cid
                    client_secret: csecret
            """);
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
              control:
                headers:
                  X-Extra: added
                verify:
                  url: https://flags.example/flags/{{featureKey}}
                  json_path: $.state
                  expected: "{{state}}"
            """);

        var control = Loader(root).LoadAll().Single().FlagProof!.Control!;

        Assert.Equal("https://flags.example/flags/{{featureKey}}", control.Url);
        Assert.Equal("Bearer static", control.Headers["Authorization"]);
        Assert.Equal("added", control.Headers["X-Extra"]);
        Assert.Equal("https://id.example/token", control.Auth!.TokenUrl);
        Assert.NotNull(control.Verify);
        Assert.Equal("$.state", control.Verify!.JsonPath);
    }

    [Fact]
    public void CaseAuthSectionReplacesManifestAuthWholesale()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: https://flags.example/f
                auth:
                  oauth2_client_credentials:
                    token_url: https://manifest.example/token
                    client_id: mcid
                    client_secret: msecret
            """);
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
              control:
                auth:
                  oauth2_client_credentials:
                    token_url: https://case.example/token
                    client_id: ccid
                    client_secret: csecret
            """);

        var auth = Loader(root).LoadAll().Single().FlagProof!.Control!.Auth!;

        Assert.Equal("https://case.example/token", auth.TokenUrl);
        Assert.Equal("ccid", auth.ClientId);
    }

    [Fact]
    public void IncompleteMergedControlBlockIsRejectedNamingTheCase()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                headers:
                  Authorization: "Bearer x"
            """);
        WriteCase(root, "broken.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var ex = Assert.Throws<CaseFileException>(() => Loader(root).LoadAll());
        Assert.Contains("broken.yaml", ex.Message);
        Assert.Contains("url", ex.Message);
    }

    [Fact]
    public void NoManifestLeavesControlNullForACaseThatDeclaresNone()
    {
        var root = CreateWorkspace();
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var loaded = Loader(root).LoadAll().Single();

        Assert.NotNull(loaded.FlagProof);
        Assert.Null(loaded.FlagProof!.Control);
    }

    [Fact]
    public void ManifestWithUnknownKeyIsRejectedNamingTheManifest()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              feature_key: checkout-v2
              control:
                method: PUT
                url: https://flags.example/f
            """);
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var ex = Assert.Throws<CaseFileException>(() => Loader(root).LoadAll());
        Assert.Contains("releasetwin.yml", ex.Message);
    }

    [Fact]
    public void ManifestWithInvalidYamlIsRejectedNamingTheManifest()
    {
        var root = CreateWorkspace();
        WriteManifest(root, "flag_proof: : :\n  - broken");
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var ex = Assert.Throws<CaseFileException>(() => Loader(root).LoadAll());
        Assert.Contains("releasetwin.yml", ex.Message);
    }

    [Fact]
    public void MissingManifestEnvVarIsAClearLoadErrorNamingTheManifest()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: ${FLAGS_API}/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer ${FLAGS_TOKEN}"
            """);
        WriteCase(root, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var ex = Assert.Throws<CaseFileException>(() => Loader(root, _ => null).LoadAll());
        Assert.Contains("releasetwin.yml", ex.Message);
        Assert.Contains("FLAGS_API", ex.Message);
    }

    [Fact]
    public void ManifestSourcedControlIsIdenticalToTheEquivalentInlineBlock()
    {
        // The flag-proof runner has no knowledge of the manifest: if the loaded FlagProofControl
        // matches the fully-inline equivalent, the run (including a failed control request →
        // ControlFailed) behaves identically. Task 3.3 / spec "A failed manifest-sourced control
        // request fails the run".
        const string control = """
              control:
                method: PUT
                url: https://flags.example/flags/{{featureKey}}
                headers:
                  Authorization: "Bearer static"
                body: '{ "on": {{enabled}} }'
                known_bad_when: enabled
                verify:
                  url: https://flags.example/flags/{{featureKey}}
                  json_path: $.on
                  expected: "{{enabled}}"
            """;

        var inlineRoot = CreateWorkspace();
        WriteCase(inlineRoot, "case1.yaml", $"""
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            {control}
            """);

        var manifestRoot = CreateWorkspace();
        WriteManifest(manifestRoot, $"flag_proof:\n{control}");
        WriteCase(manifestRoot, "case1.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var inline = Loader(inlineRoot).LoadAll().Single().FlagProof!.Control!;
        var inherited = Loader(manifestRoot).LoadAll().Single().FlagProof!.Control!;

        Assert.Equal(inline.Method, inherited.Method);
        Assert.Equal(inline.Url, inherited.Url);
        Assert.Equal(inline.Body, inherited.Body);
        Assert.Equal(inline.Polarity, inherited.Polarity);
        Assert.Equal(inline.Headers, inherited.Headers);
        Assert.Equal(inline.Verify!.Url, inherited.Verify!.Url);
        Assert.Equal(inline.Verify!.JsonPath, inherited.Verify!.JsonPath);
        Assert.Equal(inline.Verify!.Expected, inherited.Verify!.Expected);
    }

    [Fact]
    public void ShippedSharedControlExampleLoadsWithEveryCredentialResolvedFromTheEnvironment()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "examples", "cases-flag-proof-shared-control")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var casesRoot = Path.Combine(dir!.FullName, "examples", "cases-flag-proof-shared-control");
        var fixturesRoot = Path.Combine(dir.FullName, "examples", "fixtures");

        var cases = new CaseFileLoader(casesRoot, fixturesRoot, _ => "placeholder-value").LoadAll();

        Assert.Equal(new[] { "FLAGPROOF-SHARED-CHECKOUT", "FLAGPROOF-SHARED-SEARCH" }, cases.Select(c => c.Case.CaseId));
        // Both inherit the manifest's PUT + Authorization; only search-flag adds a verify.
        Assert.All(cases, c => Assert.Equal("PUT", c.FlagProof!.Control!.Method));
        Assert.All(cases, c => Assert.Equal("Bearer placeholder-value", c.FlagProof!.Control!.Headers["Authorization"]));
        Assert.Null(cases[0].FlagProof!.Control!.Verify);
        Assert.NotNull(cases[1].FlagProof!.Control!.Verify);
    }

    [Fact]
    public void ManifestFileIsNotLoadedAsACase()
    {
        var root = CreateWorkspace();
        WriteManifest(root, """
            flag_proof:
              control:
                method: PUT
                url: https://flags.example/f
            """);
        WriteCase(root, "only-case.yaml", """
            flag_proof:
              feature_key: checkout-v2
              build_identity: b1
            """);

        var cases = Loader(root).LoadAll();

        Assert.Single(cases);
        Assert.Equal("only-case", cases[0].Case.CaseId);
    }
}
