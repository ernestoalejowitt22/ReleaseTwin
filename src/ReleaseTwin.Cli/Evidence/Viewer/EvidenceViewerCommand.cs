using System.Diagnostics;
using System.Globalization;
using ReleaseTwin.Adapters.Ui;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>
/// local-evidence-viewer: <c>releasetwin view [dir] [--export &lt;file.html&gt;] [--video-dir &lt;dir&gt;]</c>.
///
/// Reads a local evidence directory, renders it, and either serves it or writes it as one
/// self-contained file. It makes no network request of any kind — no upload, no hosted API call, no
/// telemetry, no version check — needs no account and no token, and never writes into the evidence
/// directory it is reading.
/// </summary>
public static class EvidenceViewerCommand
{
    /// <summary>Where <c>view</c> looks when neither an argument nor the environment variable says.</summary>
    public const string DefaultDirectory = "evidence";

    public static async Task<int> RunAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var (options, parseError) = ParseOptions(args, environment);
        if (parseError is not null)
        {
            output.WriteLine(parseError);
            return 1;
        }

        var read = EvidenceDirectoryReader.Read(options.Directory);
        if (read.Directory is not { } directory)
        {
            output.WriteLine(read.Error);
            return 1;
        }

        directory = AttachVideos(directory, options.VideoDirectory);

        return options.ExportPath is { } exportPath
            ? Export(directory, exportPath, output)
            : await ServeAsync(directory, environment, output, cancellationToken);
    }

    public sealed record ViewOptions(string Directory, string? ExportPath, string? VideoDirectory);

    public static (ViewOptions Options, string? Error) ParseOptions(
        string[] args, IReadOnlyDictionary<string, string?> environment)
    {
        string? directory = null;
        string? exportPath = null;
        string? videoDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var (matched, value, error) = TakeValue(args, ref i, "--export");
            if (error is not null)
            {
                return (Empty, error);
            }

            if (matched)
            {
                exportPath = value;
                continue;
            }

            (matched, value, error) = TakeValue(args, ref i, "--video-dir");
            if (error is not null)
            {
                return (Empty, error);
            }

            if (matched)
            {
                videoDirectory = value;
                continue;
            }

            if (arg.StartsWith('-'))
            {
                return (Empty, $"Unrecognized option '{arg}'. Usage: releasetwin view [dir] [--export <file.html>] [--video-dir <dir>]");
            }

            if (directory is not null)
            {
                return (Empty, $"view takes one evidence directory; got '{directory}' and '{arg}'.");
            }

            directory = arg;
        }

        directory ??= Value(environment, "RELEASETWIN_EVIDENCE_DIR") ?? DefaultDirectory;
        videoDirectory ??= Value(environment, "RELEASETWIN_UI_VIDEO_DIR");

        return (new ViewOptions(directory, exportPath, videoDirectory), null);
    }

    private static readonly ViewOptions Empty = new(DefaultDirectory, null, null);

    private static (bool Matched, string? Value, string? Error) TakeValue(string[] args, ref int i, string option)
    {
        var arg = args[i];
        if (arg == option)
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            {
                return (false, null, $"{option} expects a value, e.g. {option} {(option == "--export" ? "evidence.html" : "./videos")}");
            }

            return (true, args[++i], null);
        }

        var prefix = option + "=";
        return arg.StartsWith(prefix, StringComparison.Ordinal)
            ? (true, arg[prefix.Length..], null)
            : (false, null, null);
    }

    private static string? Value(IReadOnlyDictionary<string, string?> environment, string key) =>
        environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// Matches each case to <c>&lt;video-dir&gt;/&lt;sanitized-case-id&gt;.webm</c> using the recorder's
    /// own naming rule (design D4). Nothing in the evidence document references a recording, so the
    /// directory has to be supplied; a case with no matching file is simply left without video.
    /// </summary>
    public static EvidenceDirectoryView AttachVideos(EvidenceDirectoryView directory, string? videoDirectory)
    {
        if (videoDirectory is null || !Directory.Exists(videoDirectory))
        {
            return directory;
        }

        var cases = directory.Cases
            .Select(c =>
            {
                var path = SessionRecordingFile.PathFor(videoDirectory, c.CaseId);
                return File.Exists(path) ? c with { VideoPath = path } : c;
            })
            .ToList();

        return directory with { Cases = cases };
    }

    private static int Export(EvidenceDirectoryView directory, string exportPath, TextWriter output)
    {
        var html = EvidenceReportRenderer.Render(directory, new InlinedAssetLinks());

        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(exportPath));
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(exportPath, html);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            output.WriteLine($"Could not write '{exportPath}': {ex.Message}");
            return 1;
        }

        output.WriteLine($"Wrote {Path.GetFullPath(exportPath)} ({Count(directory.Cases.Count, "case")}).");
        return 0;
    }

    private static async Task<int> ServeAsync(
        EvidenceDirectoryView directory,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var html = EvidenceReportRenderer.Render(directory, new ServedAssetLinks());
        var containerized = IsContainerized(environment);

        EvidenceReportServer server;
        try
        {
            server = EvidenceReportServer.Start(directory, html, bindAllInterfaces: containerized);
        }
        catch (Exception ex) when (ex is IOException or System.Net.HttpListenerException)
        {
            output.WriteLine($"Could not serve the evidence report: {ex.Message}");
            output.WriteLine("Write it to a file instead:  releasetwin view <dir> --export evidence.html");
            return 1;
        }

        using (server)
        {
            output.WriteLine($"Serving {Count(directory.Cases.Count, "case")} from {directory.SourcePath}");
            output.WriteLine($"Open {server.Url}");
            if (containerized)
            {
                output.WriteLine($"(running in a container — reachable from the host only if you published the port, e.g. -p {server.Port.ToString(CultureInfo.InvariantCulture)}:{server.Port.ToString(CultureInfo.InvariantCulture)})");
            }
            else
            {
                TryOpenBrowser(server.Url);
            }

            output.WriteLine("Press Ctrl+C to stop.");

            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsoleCancelEventHandler? onCancel = null;
            onCancel = (_, e) =>
            {
                e.Cancel = true;
                stopping.Cancel();
            };

            try
            {
                Console.CancelKeyPress += onCancel;
                await server.RunAsync(stopping.Token);
            }
            finally
            {
                Console.CancelKeyPress -= onCancel;
            }
        }

        return 0;
    }

    /// <summary>
    /// The Microsoft base images set <c>DOTNET_RUNNING_IN_CONTAINER</c>; <c>/.dockerenv</c> covers
    /// images that do not.
    /// </summary>
    public static bool IsContainerized(IReadOnlyDictionary<string, string?> environment) =>
        Value(environment, "DOTNET_RUNNING_IN_CONTAINER") is "1" or "true" or "True"
        || File.Exists("/.dockerenv");

    /// <summary>
    /// Opening a browser is a convenience, never the contract: the URL is printed either way, and a
    /// failure here is not a failure of the command.
    /// </summary>
    private static void TryOpenBrowser(string url)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // No browser, no desktop session, no shell handler — the printed URL still works.
        }
    }

    private static string Count(int n, string noun) =>
        $"{n.ToString(CultureInfo.InvariantCulture)} {noun}{(n == 1 ? string.Empty : "s")}";
}
