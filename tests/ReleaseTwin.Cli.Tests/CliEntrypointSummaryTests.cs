using System.Text.Json;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-pr-integration: `--summary-json` flag parsing — file written, env fallback, bad directory error, arg stripping.</summary>
public class CliEntrypointSummaryTests
{
    private static string WorkspaceWithPassingCase()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-entrypoint-summary-").FullName;
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
    public async Task SummaryJsonFlagWritesTheFileAndDoesNotBreakTheDirectoryArgument()
    {
        var ws = WorkspaceWithPassingCase();
        var cases = Path.Combine(ws, "cases");
        var summaryPath = Path.Combine(ws, "summary.json");

        var exit = await CliEntrypoint.RunAsync(
            new[] { "run", cases, "--summary-json", summaryPath }, new Dictionary<string, string?>(), new StringWriter());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(summaryPath));
        Assert.Equal(2, JsonDocument.Parse(File.ReadAllText(summaryPath)).RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task EnvironmentVariableIsAFallbackForTheFlag()
    {
        var ws = WorkspaceWithPassingCase();
        var cases = Path.Combine(ws, "cases");
        var summaryPath = Path.Combine(ws, "from-env.json");

        var exit = await CliEntrypoint.RunAsync(
            new[] { cases },
            new Dictionary<string, string?> { ["RELEASETWIN_SUMMARY_JSON"] = summaryPath },
            new StringWriter());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(summaryPath));
    }

    [Fact]
    public async Task ANonExistentDirectoryIsAClearErrorAndNothingRuns()
    {
        var ws = WorkspaceWithPassingCase();
        var cases = Path.Combine(ws, "cases");
        var bad = Path.Combine(ws, "no", "such", "dir", "out.json");
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(
            new[] { "run", cases, "--summary-json", bad }, new Dictionary<string, string?>(), output);

        Assert.Equal(1, exit);
        Assert.Contains("--summary-json", output.ToString());
        Assert.DoesNotContain("PASS PING-1", output.ToString());
    }
}
