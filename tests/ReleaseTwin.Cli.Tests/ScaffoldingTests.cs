using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Cli.Scaffolding;

namespace ReleaseTwin.Cli.Tests;

public class ScaffoldingTests
{
    private static string TempDir() => Directory.CreateTempSubdirectory("releasetwin-scaffold-").FullName;

    [Fact]
    public void Init_writes_the_expected_tree()
    {
        var dir = TempDir();
        var output = new StringWriter();

        var code = new ScaffoldWriter(output).Init(dir);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(dir, "cases", "starter.yaml")));
        Assert.True(File.Exists(Path.Combine(dir, "fixtures", "starter.json")));
        Assert.True(File.Exists(Path.Combine(dir, "releasetwin.yaml")));
        var gitignore = File.ReadAllText(Path.Combine(dir, ".gitignore"));
        Assert.Contains("/.releasetwin/", gitignore);
        Assert.Contains("id: starter", File.ReadAllText(Path.Combine(dir, "cases", "starter.yaml")));
    }

    [Fact]
    public void Scaffolded_fixture_hash_matches_the_case_files_recorded_hash()
    {
        var dir = TempDir();
        new ScaffoldWriter(new StringWriter()).Init(dir);

        var fixtureBytes = File.ReadAllBytes(Path.Combine(dir, "fixtures", "starter.json"));
        var actual = Convert.ToHexString(SHA256.HashData(fixtureBytes)).ToLowerInvariant();

        var caseYaml = File.ReadAllText(Path.Combine(dir, "cases", "starter.yaml"));
        var recorded = caseYaml
            .Split('\n')
            .Select(l => l.Trim())
            .First(l => l.StartsWith("sha256:", StringComparison.Ordinal))
            .Substring("sha256:".Length)
            .Trim();

        Assert.Equal(recorded, actual);
    }

    [Fact]
    public async Task Init_then_run_is_green_with_no_environment()
    {
        var dir = TempDir();
        new ScaffoldWriter(new StringWriter()).Init(dir);

        var runOutput = new StringWriter();
        var code = await CliEntrypoint.RunAsync(
            new[] { "run", Path.Combine(dir, "cases") },
            new Dictionary<string, string?>(),
            runOutput);

        Assert.Equal(0, code);
        Assert.Contains("PASS starter", runOutput.ToString());
        Assert.Contains("1 passed, 0 failed", runOutput.ToString());
    }

    [Fact]
    public void Init_refuses_on_an_already_initialized_project_and_writes_nothing()
    {
        var dir = TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "cases"));
        File.WriteAllText(Path.Combine(dir, "cases", "mine.yaml"), "id: MINE\n");
        var before = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToHashSet();
        var output = new StringWriter();

        var code = new ScaffoldWriter(output).Init(dir);

        Assert.Equal(1, code);
        Assert.Contains("already", output.ToString());
        Assert.Equal(before, Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToHashSet());
    }

    [Fact]
    public void New_adds_a_case_and_refuses_to_clobber()
    {
        var dir = TempDir();
        new ScaffoldWriter(new StringWriter()).Init(dir);

        var first = new ScaffoldWriter(new StringWriter()).New(dir, "ORDERS-1");
        Assert.Equal(0, first);
        Assert.True(File.Exists(Path.Combine(dir, "cases", "ORDERS-1.yaml")));
        Assert.Contains("id: ORDERS-1", File.ReadAllText(Path.Combine(dir, "cases", "ORDERS-1.yaml")));

        var original = File.ReadAllText(Path.Combine(dir, "cases", "ORDERS-1.yaml"));
        var output = new StringWriter();
        var second = new ScaffoldWriter(output).New(dir, "ORDERS-1");

        Assert.Equal(1, second);
        Assert.Contains("already exists", output.ToString());
        Assert.Equal(original, File.ReadAllText(Path.Combine(dir, "cases", "ORDERS-1.yaml")));
    }

    [Fact]
    public void Gitignore_append_is_idempotent()
    {
        var dir = TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "cases"));
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "/.releasetwin/\nnode_modules/\n");

        // New() doesn't touch .gitignore; drive Init on a fresh dir that already has the line.
        var dir2 = TempDir();
        File.WriteAllText(Path.Combine(dir2, ".gitignore"), "/.releasetwin/\n");
        new ScaffoldWriter(new StringWriter()).Init(dir2);

        var lines = File.ReadAllLines(Path.Combine(dir2, ".gitignore"));
        Assert.Single(lines, l => l.Trim() == "/.releasetwin/");
    }

    [Fact]
    public void FromExamples_errors_clearly_when_the_bundled_path_is_absent()
    {
        var dir = TempDir();
        var output = new StringWriter();

        var code = new ScaffoldWriter(output, bundledExamplesPath: Path.Combine(dir, "no-such-examples"))
            .Init(dir, fromExamples: true);

        Assert.Equal(1, code);
        Assert.Contains("releasetwin init", output.ToString());
    }

    [Fact]
    public void FromExamples_copies_the_bundled_tree_when_present()
    {
        var bundle = TempDir();
        Directory.CreateDirectory(Path.Combine(bundle, "cases"));
        File.WriteAllText(Path.Combine(bundle, "cases", "e.yaml"), "id: E\n");
        File.WriteAllText(Path.Combine(bundle, "fixtures.json"), "{}");

        var dir = TempDir();
        var code = new ScaffoldWriter(new StringWriter(), bundledExamplesPath: bundle).Init(dir, fromExamples: true);

        Assert.Equal(0, code);
        Assert.Equal("id: E\n", File.ReadAllText(Path.Combine(dir, "cases", "e.yaml")));
        Assert.True(File.Exists(Path.Combine(dir, "fixtures.json")));
    }
}
