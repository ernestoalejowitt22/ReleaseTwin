using System.Xml.Linq;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-report-formats: `--junit-xml` flag parsing — file written, env fallback, argument wins, bad directory error, nothing without the flag.</summary>
public class CliEntrypointJUnitTests
{
    private static string WorkspaceWithPassingCase()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-entrypoint-junit-").FullName;
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
    public async Task JunitXmlFlagWritesTheFileAndDoesNotBreakTheDirectoryArgument()
    {
        var ws = WorkspaceWithPassingCase();
        var junitPath = Path.Combine(ws, "junit.xml");

        var exit = await CliEntrypoint.RunAsync(
            new[] { "run", Path.Combine(ws, "cases"), "--junit-xml", junitPath }, new Dictionary<string, string?>(), new StringWriter());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(junitPath));
        Assert.Equal("testsuites", XDocument.Load(junitPath).Root!.Name.LocalName);
    }

    [Fact]
    public async Task EnvironmentVariableIsAFallbackForTheFlag()
    {
        var ws = WorkspaceWithPassingCase();
        var junitPath = Path.Combine(ws, "from-env.xml");

        var exit = await CliEntrypoint.RunAsync(
            new[] { Path.Combine(ws, "cases") },
            new Dictionary<string, string?> { ["RELEASETWIN_JUNIT_XML"] = junitPath },
            new StringWriter());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(junitPath));
    }

    [Fact]
    public async Task TheArgumentWinsOverTheEnvironmentVariable()
    {
        var ws = WorkspaceWithPassingCase();
        var fromArg = Path.Combine(ws, "from-arg.xml");
        var fromEnv = Path.Combine(ws, "from-env.xml");

        var exit = await CliEntrypoint.RunAsync(
            new[] { "run", Path.Combine(ws, "cases"), "--junit-xml", fromArg },
            new Dictionary<string, string?> { ["RELEASETWIN_JUNIT_XML"] = fromEnv },
            new StringWriter());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(fromArg));
        Assert.False(File.Exists(fromEnv));
    }

    [Fact]
    public async Task NoFlagMeansNoFileAndUnchangedOutput()
    {
        var ws = WorkspaceWithPassingCase();
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(
            new[] { Path.Combine(ws, "cases") }, new Dictionary<string, string?>(), output);

        Assert.Equal(0, exit);
        Assert.Empty(Directory.GetFiles(ws, "*.xml"));
        Assert.Contains("PASS PING-1", output.ToString());
    }

    [Fact]
    public async Task ANonExistentDirectoryIsAClearErrorAndNothingRuns()
    {
        var ws = WorkspaceWithPassingCase();
        var bad = Path.Combine(ws, "no", "such", "dir", "junit.xml");
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(
            new[] { "run", Path.Combine(ws, "cases"), "--junit-xml", bad }, new Dictionary<string, string?>(), output);

        Assert.Equal(1, exit);
        Assert.Contains("--junit-xml", output.ToString());
        Assert.DoesNotContain("PASS PING-1", output.ToString());
    }
}
