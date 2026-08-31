using ReleaseTwin.Cli.Scaffolding;

namespace ReleaseTwin.Cli;

/// <summary>
/// releasetwin-init-scaffold: arg dispatch, extracted from <c>Program.cs</c> so it is testable.
///
/// <list type="bullet">
///   <item><c>init</c> / <c>new &lt;case-id&gt;</c> — scaffolding (never destructive)</item>
///   <item><c>run [dir] [--journey &lt;id&gt;@&lt;v&gt;]</c> — execute cases</item>
///   <item>no recognized subcommand — the pre-subcommand behaviour: a leading
///     <c>--journey &lt;id&gt;@&lt;v&gt;</c> runs that pinned journey, otherwise the first arg
///     (or <c>cases</c>) is the directory to execute</item>
///   <item><c>--help</c> / <c>-h</c> — usage</item>
/// </list>
/// </summary>
public static class CliEntrypoint
{
    public static Task<int> RunAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CliRunner? runner = null,
        ScaffoldWriter? scaffolder = null)
    {
        runner ??= new CliRunner();

        var head = args.Length > 0 ? args[0] : null;

        if (head is "--help" or "-h" or "help")
        {
            PrintUsage(output);
            return Task.FromResult(0);
        }

        if (head == "init")
        {
            scaffolder ??= new ScaffoldWriter(output);
            var fromExamples = args.Contains("--from-examples");
            var dir = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-')) ?? Directory.GetCurrentDirectory();
            return Task.FromResult(scaffolder.Init(dir, fromExamples));
        }

        if (head == "new")
        {
            scaffolder ??= new ScaffoldWriter(output);
            var caseId = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
            if (caseId is null)
            {
                output.WriteLine("Usage: releasetwin new <case-id>");
                return Task.FromResult(1);
            }

            return Task.FromResult(scaffolder.New(Directory.GetCurrentDirectory(), caseId));
        }

        // `run` — same behaviour as no subcommand, just with the leading `run` stripped.
        var runArgs = head == "run" ? args.Skip(1).ToArray() : args;
        return ExecuteAsync(runArgs, environment, output, runner);
    }

    private static Task<int> ExecuteAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CliRunner runner)
    {
        if (args.Length >= 2 && args[0] == "--journey")
        {
            var parts = args[1].Split('@', 2);
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var journeyId) || !int.TryParse(parts[1], out var version))
            {
                output.WriteLine("--journey expects <journeyId>@<version>, e.g. --journey 3fa85f64-5717-4562-b3fc-2c963f66afa6@3");
                return Task.FromResult(1);
            }

            return runner.RunJourneyAsync(journeyId, version, environment, output);
        }

        var casesDirectory = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "cases";
        return runner.RunAsync(casesDirectory, environment, output);
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("""
            releasetwin — release-proof testing

              releasetwin init [--from-examples]   scaffold a project in the current directory
              releasetwin new <case-id>            add one more case + fixture to this project
              releasetwin run [dir]                run cases (default: ./cases)
              releasetwin run --journey <id>@<v>   run a pinned hosted journey

            `releasetwin <dir>` and `releasetwin --journey <id>@<v>` still work without `run`.
            """);
    }
}
