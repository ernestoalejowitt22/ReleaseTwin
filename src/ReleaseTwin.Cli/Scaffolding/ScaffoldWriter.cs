using System.Reflection;

namespace ReleaseTwin.Cli.Scaffolding;

/// <summary>
/// releasetwin-init-scaffold: writes a runnable starter project (or one more case) into a
/// directory. Never modifies or deletes an existing file — <see cref="Init"/> refuses when the
/// project already has a case, <see cref="New"/> refuses to clobber its targets, and
/// <c>.gitignore</c> is only ever appended to.
/// </summary>
public sealed class ScaffoldWriter
{
    private const string BundledExamplesPath = "/opt/releasetwin/examples";

    private static readonly string[] GitignoreLines =
    {
        "# ReleaseTwin local run output",
        "/.releasetwin/",
        "*.releasetwin.local",
    };

    private readonly TextWriter _output;
    private readonly string _bundledExamplesPath;

    public ScaffoldWriter(TextWriter output, string? bundledExamplesPath = null)
    {
        _output = output;
        _bundledExamplesPath = bundledExamplesPath ?? BundledExamplesPath;
    }

    /// <summary>
    /// Initialize <paramref name="directory"/> with a starter case, its fixture, a
    /// <c>releasetwin.yaml</c>, and <c>.gitignore</c> entries. Returns a process exit code.
    /// </summary>
    public int Init(string directory, bool fromExamples = false)
    {
        var casesDir = Path.Combine(directory, "cases");
        if (Directory.Exists(casesDir) && Directory.EnumerateFiles(casesDir, "*.yaml").Any())
        {
            _output.WriteLine($"'{casesDir}' already has a case file — this project looks initialized. Nothing written.");
            return 1;
        }

        if (fromExamples)
        {
            return InitFromExamples(directory);
        }

        Directory.CreateDirectory(casesDir);
        Directory.CreateDirectory(Path.Combine(directory, "fixtures"));

        WriteNew(Path.Combine(casesDir, "starter.yaml"), Substitute(ReadTemplate("case.yaml"), "starter"));
        WriteNew(Path.Combine(directory, "fixtures", "starter.json"), ReadTemplate("fixture.json"));
        WriteNew(Path.Combine(directory, "releasetwin.yaml"), ReadTemplate("releasetwin.yaml"));
        AppendGitignore(directory);

        _output.WriteLine("Wrote cases/starter.yaml, fixtures/starter.json, releasetwin.yaml, .gitignore.");
        _output.WriteLine("Run it:  releasetwin run");
        return 0;
    }

    /// <summary>
    /// Add one case + fixture pair named <paramref name="caseId"/> to an existing project.
    /// </summary>
    public int New(string directory, string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId) || caseId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            _output.WriteLine($"'{caseId}' is not a usable case id.");
            return 1;
        }

        var caseFile = Path.Combine(directory, "cases", $"{caseId}.yaml");
        var fixtureFile = Path.Combine(directory, "fixtures", $"{caseId}.json");

        if (File.Exists(caseFile) || File.Exists(fixtureFile))
        {
            _output.WriteLine($"'{(File.Exists(caseFile) ? caseFile : fixtureFile)}' already exists. Nothing written.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(caseFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(fixtureFile)!);
        WriteNew(caseFile, Substitute(ReadTemplate("case.yaml"), caseId));
        WriteNew(fixtureFile, ReadTemplate("fixture.json"));

        _output.WriteLine($"Wrote cases/{caseId}.yaml, fixtures/{caseId}.json.");
        return 0;
    }

    private int InitFromExamples(string directory)
    {
        if (!Directory.Exists(_bundledExamplesPath))
        {
            _output.WriteLine(
                $"No bundled examples at '{_bundledExamplesPath}' — that path only exists inside the container image. " +
                "Run plain 'releasetwin init', or clone the repo for the full examples/ tree.");
            return 1;
        }

        var conflicts = Directory.EnumerateFiles(_bundledExamplesPath, "*", SearchOption.AllDirectories)
            .Select(src => Path.Combine(directory, Path.GetRelativePath(_bundledExamplesPath, src)))
            .Where(File.Exists)
            .ToList();
        if (conflicts.Count > 0)
        {
            _output.WriteLine($"Would overwrite {conflicts.Count} existing file(s) (e.g. {conflicts[0]}). Nothing written.");
            return 1;
        }

        foreach (var src in Directory.EnumerateFiles(_bundledExamplesPath, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(directory, Path.GetRelativePath(_bundledExamplesPath, src));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest);
        }

        AppendGitignore(directory);
        _output.WriteLine($"Copied the bundled examples/ tree into '{directory}'.");
        return 0;
    }

    private static void WriteNew(string path, string content) => File.WriteAllText(path, content);

    private void AppendGitignore(string directory)
    {
        var path = Path.Combine(directory, ".gitignore");
        var existing = File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.TrimEnd()).ToHashSet()
            : new HashSet<string>();

        var toAdd = GitignoreLines.Where(l => !existing.Contains(l)).ToList();
        if (toAdd.Count == 0)
        {
            return;
        }

        var prefix = File.Exists(path) && new FileInfo(path).Length > 0 ? Environment.NewLine : string.Empty;
        File.AppendAllText(path, prefix + string.Join(Environment.NewLine, toAdd) + Environment.NewLine);
    }

    private static string Substitute(string template, string caseId) => template.Replace("{{caseId}}", caseId);

    private static string ReadTemplate(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = $"ReleaseTwin.Cli.Scaffolding.Templates.{name}";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded template '{resource}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
