namespace ReleaseTwin.Cli.Tests;

public class CliRunnerConfigTests
{
    private static string Workspace(string? releasetwinYaml)
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-cli-config-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        if (releasetwinYaml is not null)
        {
            File.WriteAllText(Path.Combine(root, "releasetwin.yaml"), releasetwinYaml);
        }

        return root;
    }

    private static void WritePassingHttpCase(string root)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", "C.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "C.yaml"), """
            id: HTTP-CFG-1
            oracle:
              locator: t/HTTP-CFG-1
            fixture:
              locator: C.json
            pipeline:
              - operation: http.request
                with:
                  method: GET
                  url: https://jsonplaceholder.typicode.com/posts/1
              - operation: http.assertJsonPath
                with:
                  path: $.id
                  expected: 1
            """);
    }

    [Fact]
    public async Task Adapters_http_only_runs_the_http_case_green()
    {
        var root = Workspace("adapters:\n  - http\n");
        WritePassingHttpCase(root);
        var output = new StringWriter();

        var code = await new CliRunner().RunAsync(Path.Combine(root, "cases"), new Dictionary<string, string?>(), output);

        Assert.Equal(0, code);
        Assert.Contains("PASS HTTP-CFG-1", output.ToString());
    }

    [Fact]
    public async Task Listed_adapter_with_no_credentials_is_a_startup_error()
    {
        var root = Workspace("adapters:\n  - http\n  - launchdarkly\n");
        WritePassingHttpCase(root);
        var output = new StringWriter();

        var code = await new CliRunner().RunAsync(Path.Combine(root, "cases"), new Dictionary<string, string?>(), output);

        Assert.NotEqual(0, code);
        Assert.Contains("releasetwin.yaml lists 'launchdarkly'", output.ToString());
        Assert.DoesNotContain("PASS", output.ToString());
    }

    [Fact]
    public async Task Env_configured_but_unlisted_adapter_is_not_installed()
    {
        // Azure DevOps fully configured in the environment, but omitted from the list.
        var root = Workspace("adapters:\n  - http\n");
        File.WriteAllText(Path.Combine(root, "fixtures", "A.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "A.yaml"), """
            id: AZDO-CFG-1
            oracle:
              locator: t/AZDO-CFG-1
            fixture:
              locator: A.json
            pipeline:
              - operation: azdo.createWorkItem
            """);
        var env = new Dictionary<string, string?>
        {
            ["AZDO_ORG"] = "o",
            ["AZDO_PROJECT"] = "P",
            ["AZDO_PAT"] = "p",
            ["AZDO_AREA_PATH"] = "P\\A",
            ["AZDO_VARIABLE_GROUP_ID"] = "1",
        };
        var output = new StringWriter();

        var code = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler());

        Assert.NotEqual(0, code);
        Assert.Contains("missing-capability:http:azure-devops", output.ToString());
    }

    [Fact]
    public async Task Partial_env_config_is_still_a_startup_error_even_when_unlisted()
    {
        var root = Workspace("adapters:\n  - http\n");
        WritePassingHttpCase(root);
        var env = new Dictionary<string, string?> { ["AZDO_ORG"] = "only-one-of-five" };
        var output = new StringWriter();

        var code = await new CliRunner().RunAsync(Path.Combine(root, "cases"), env, output);

        Assert.NotEqual(0, code);
        Assert.Contains("Azure DevOps is partially configured", output.ToString());
    }

    [Fact]
    public async Task Malformed_releasetwin_yaml_is_a_startup_error()
    {
        var root = Workspace("adapters:\n  - http\n  - bogus-adapter\n");
        WritePassingHttpCase(root);
        var output = new StringWriter();

        var code = await new CliRunner().RunAsync(Path.Combine(root, "cases"), new Dictionary<string, string?>(), output);

        Assert.NotEqual(0, code);
        Assert.Contains("bogus-adapter", output.ToString());
    }
}
