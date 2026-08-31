namespace ReleaseTwin.Cli.Tests;

public class CliEntrypointTests
{
    private static Dictionary<string, string?> Env() => new();

    private static string WorkspaceWithPassingCase()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-entrypoint-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "C.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "C.yaml"), """
            id: PING-1
            oracle:
              locator: t/PING-1
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
        return root;
    }

    [Fact]
    public async Task Run_dir_equals_bare_dir()
    {
        var ws = WorkspaceWithPassingCase();
        var cases = Path.Combine(ws, "cases");

        var withRun = new StringWriter();
        var a = await CliEntrypoint.RunAsync(new[] { "run", cases }, Env(), withRun);

        var bare = new StringWriter();
        var b = await CliEntrypoint.RunAsync(new[] { cases }, Env(), bare);

        Assert.Equal(a, b);
        Assert.Equal(0, a);
        Assert.Contains("PASS PING-1", withRun.ToString());
        Assert.Contains("PASS PING-1", bare.ToString());
    }

    [Fact]
    public async Task Journey_arg_works_with_or_without_the_run_prefix()
    {
        // No token configured -> RunJourneyAsync returns the same clear failure either way.
        var withRun = new StringWriter();
        var a = await CliEntrypoint.RunAsync(
            new[] { "run", "--journey", "3fa85f64-5717-4562-b3fc-2c963f66afa6@1" }, Env(), withRun);

        var without = new StringWriter();
        var b = await CliEntrypoint.RunAsync(
            new[] { "--journey", "3fa85f64-5717-4562-b3fc-2c963f66afa6@1" }, Env(), without);

        Assert.Equal(a, b);
        Assert.NotEqual(0, a);
        Assert.Equal(withRun.ToString(), without.ToString());
        Assert.Contains("RELEASETWIN_API_TOKEN", withRun.ToString());
    }

    [Fact]
    public async Task Malformed_journey_arg_is_rejected()
    {
        var output = new StringWriter();
        var code = await CliEntrypoint.RunAsync(new[] { "run", "--journey", "not-a-journey" }, Env(), output);

        Assert.Equal(1, code);
        Assert.Contains("--journey expects", output.ToString());
    }

    [Fact]
    public async Task Help_lists_the_subcommands()
    {
        foreach (var flag in new[] { "--help", "-h", "help" })
        {
            var output = new StringWriter();
            var code = await CliEntrypoint.RunAsync(new[] { flag }, Env(), output);

            Assert.Equal(0, code);
            var text = output.ToString();
            Assert.Contains("init", text);
            Assert.Contains("new <case-id>", text);
            Assert.Contains("run", text);
        }
    }

    [Fact]
    public async Task Init_dispatches_to_scaffolding()
    {
        var dir = Directory.CreateTempSubdirectory("releasetwin-entrypoint-init-").FullName;
        var output = new StringWriter();

        var code = await CliEntrypoint.RunAsync(new[] { "init", dir }, Env(), output);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(dir, "cases", "starter.yaml")));
    }

    [Fact]
    public async Task New_without_an_id_prints_usage()
    {
        var output = new StringWriter();
        var code = await CliEntrypoint.RunAsync(new[] { "new" }, Env(), output);

        Assert.Equal(1, code);
        Assert.Contains("releasetwin new <case-id>", output.ToString());
    }
}
