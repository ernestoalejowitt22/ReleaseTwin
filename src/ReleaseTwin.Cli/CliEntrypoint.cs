using ReleaseTwin.Cli.Evidence.Viewer;
using ReleaseTwin.Cli.Scaffolding;

namespace ReleaseTwin.Cli;

/// <summary>
/// releasetwin-init-scaffold: arg dispatch, extracted from <c>Program.cs</c> so it is testable.
///
/// <list type="bullet">
///   <item><c>init</c> / <c>new &lt;case-id&gt;</c> — scaffolding (never destructive)</item>
///   <item><c>run [dir] [--journey &lt;id&gt;@&lt;v&gt;]</c> — execute cases</item>
///   <item><c>view [dir] [--export &lt;file&gt;] [--video-dir &lt;dir&gt;]</c> — render local evidence</item>
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

        // local-evidence-viewer: `view` must be matched *before* the fallthrough below, which treats
        // any unrecognized head as a directory of cases to run — a missing branch here would make
        // `view ./evidence` silently attempt a run against ./evidence instead of rendering it.
        if (head == "view")
        {
            return EvidenceViewerCommand.RunAsync(args.Skip(1).ToArray(), environment, output);
        }

        // `run` — same behaviour as no subcommand, just with the leading `run` stripped.
        var runArgs = head == "run" ? args.Skip(1).ToArray() : args;

        // ci-pr-integration / ci-report-formats: `--summary-json <path>` and `--junit-xml <path>`
        // (flag wins over the matching env var) are lifted out of the args here and threaded to the
        // run loop as environment values, so no run-path plumbing changes. design.md D-A / D-B.
        var (cleanedArgs, summaryPath, summaryError) = ExtractReportPath(
            runArgs, environment, "--summary-json", "summary.json", "RELEASETWIN_SUMMARY_JSON", RunSummaryWriter.ValidateDestination);
        if (summaryError is not null)
        {
            output.WriteLine(summaryError);
            return Task.FromResult(1);
        }

        var (finalArgs, junitPath, junitError) = ExtractReportPath(
            cleanedArgs, environment, "--junit-xml", "junit.xml", "RELEASETWIN_JUNIT_XML", JUnitReportWriter.ValidateDestination);
        if (junitError is not null)
        {
            output.WriteLine(junitError);
            return Task.FromResult(1);
        }

        IReadOnlyDictionary<string, string?> effectiveEnvironment = environment;
        if (summaryPath is not null || junitPath is not null)
        {
            var mutable = new Dictionary<string, string?>(environment.ToDictionary(kv => kv.Key, kv => kv.Value));
            if (summaryPath is not null)
            {
                mutable["RELEASETWIN_SUMMARY_JSON"] = summaryPath;
            }

            if (junitPath is not null)
            {
                mutable["RELEASETWIN_JUNIT_XML"] = junitPath;
            }

            effectiveEnvironment = mutable;
        }

        return ExecuteAsync(finalArgs, effectiveEnvironment, output, runner);
    }

    private static (string[] Args, string? Path, string? Error) ExtractReportPath(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        string optionName,
        string exampleFileName,
        string environmentVariable,
        Func<string, string?> validateDestination)
    {
        var prefix = optionName + "=";
        string? path = null;
        var cleaned = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == optionName)
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                {
                    return (args, null, $"{optionName} expects a file path, e.g. {optionName} {exampleFileName}");
                }

                path = args[++i];
                continue;
            }

            if (args[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                path = args[i][prefix.Length..];
                continue;
            }

            cleaned.Add(args[i]);
        }

        path ??= environment.TryGetValue(environmentVariable, out var fromEnv) && !string.IsNullOrWhiteSpace(fromEnv)
            ? fromEnv
            : null;

        if (path is not null && validateDestination(path) is { } error)
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
              releasetwin view [dir]               open this run's local evidence in a browser
                                                   (default: $RELEASETWIN_EVIDENCE_DIR, else ./evidence)

            run options:
              --summary-json <path>               also write a machine-readable JSON run summary
                                                  (or set RELEASETWIN_SUMMARY_JSON)
              --junit-xml <path>                  also write a JUnit XML test report for CI test
                                                  widgets (or set RELEASETWIN_JUNIT_XML)

            view options:
              --export <file.html>                write one self-contained file instead of serving
                                                  — no port to publish, opens straight off disk
              --video-dir <dir>                   look for session recordings there (or set
                                                  RELEASETWIN_UI_VIDEO_DIR); recordings are not in
                                                  the evidence directory

            In a container, publish the port and open the printed URL on your host — the image has
            no browser of its own:
              docker run --rm -p 8080:8080 -v "$PWD:/workspace" <image> view /workspace/evidence

            `releasetwin <dir>` and `releasetwin --journey <id>@<v>` still work without `run`.
            """);
    }
}
