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

        // ci-pr-integration: `--summary-json <path>` (flag wins over RELEASETWIN_SUMMARY_JSON) is
        // lifted out of the args here and threaded to the run loop as an environment value, so no
        // run-path plumbing changes. design.md D-A / D-B.
        var (cleanedArgs, summaryPath, summaryError) = ExtractSummaryJson(runArgs, environment);
        if (summaryError is not null)
        {
            output.WriteLine(summaryError);
            return Task.FromResult(1);
        }

        var effectiveEnvironment = summaryPath is null
            ? environment
            : new Dictionary<string, string?>(environment.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                ["RELEASETWIN_SUMMARY_JSON"] = summaryPath,
            };

        return ExecuteAsync(cleanedArgs, effectiveEnvironment, output, runner);
    }

    private static (string[] Args, string? SummaryPath, string? Error) ExtractSummaryJson(
        string[] args, IReadOnlyDictionary<string, string?> environment)
    {
        string? path = null;
        var cleaned = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--summary-json")
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                {
                    return (args, null, "--summary-json expects a file path, e.g. --summary-json summary.json");
                }

                path = args[++i];
                continue;
            }

            if (args[i].StartsWith("--summary-json=", StringComparison.Ordinal))
            {
                path = args[i]["--summary-json=".Length..];
                continue;
            }

            cleaned.Add(args[i]);
        }

        path ??= environment.TryGetValue("RELEASETWIN_SUMMARY_JSON", out var fromEnv) && !string.IsNullOrWhiteSpace(fromEnv)
            ? fromEnv
            : null;

        if (path is not null && RunSummaryWriter.ValidateDestination(path) is { } error)
        {
            return (args, null, error);
        }

        return (cleaned.ToArray(), path, null);
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

            run options:
              --summary-json <path>               also write a machine-readable JSON run summary
                                                  (or set RELEASETWIN_SUMMARY_JSON)

            `releasetwin <dir>` and `releasetwin --journey <id>@<v>` still work without `run`.
            """);
    }
}
