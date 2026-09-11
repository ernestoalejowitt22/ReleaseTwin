namespace ReleaseTwin.Cli.Upload;

/// <summary>
/// junit-upload-verb: `releasetwin upload-junit &lt;file&gt; [--release &lt;label&gt;]` — uploads a JUnit
/// XML report a suite already wrote to the hosted platform, so a team with an existing Playwright,
/// pytest or Jest suite gets run history without authoring a case file.
///
/// The file is sent verbatim. Nothing here parses, validates or reformats it: the hosted parser is
/// the single place JUnit dialect differences are interpreted (design D3), so an unfamiliar dialect
/// is the platform's call to accept or reject, not the CLI's.
///
/// Unlike an upload during a run — where `cli-runner` specifies that a failure is a warning which
/// leaves the run's own result and exit code alone — here the upload IS the command, so every
/// failure path exits non-zero (design D4).
/// </summary>
public static class JUnitUploadCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        HttpMessageHandler? handlerForTesting = null,
        CancellationToken cancellationToken = default)
    {
        var (options, parseError) = ParseOptions(args);
        if (parseError is not null)
        {
            output.WriteLine(parseError);
            output.WriteLine("Usage: releasetwin upload-junit <file> [--release <label>]");
            return 1;
        }

        var apiToken = Get(environment, "RELEASETWIN_API_TOKEN");
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            // Deliberately an error, not a silent no-op: a run without a token simply skips its
            // optional upload, but this command has nothing else to do.
            output.WriteLine("upload-junit requires RELEASETWIN_API_TOKEN to be set.");
            return 1;
        }

        // Read before any network call, so a wrong path fails locally and names what the user typed.
        if (!File.Exists(options!.FilePath))
        {
            output.WriteLine($"upload-junit: file not found: {options.FilePath}");
            return 1;
        }

        byte[] xml;
        try
        {
            xml = await File.ReadAllBytesAsync(options.FilePath, cancellationToken);
        }
        catch (IOException ex)
        {
            output.WriteLine($"upload-junit: could not read {options.FilePath}: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            output.WriteLine($"upload-junit: could not read {options.FilePath}: {ex.Message}");
            return 1;
        }

        var baseUrl = Get(environment, "RELEASETWIN_API_URL") is { Length: > 0 } url
            ? url
            : "https://api.releasetwin.example";

        using var client = new IngestClient(baseUrl, apiToken, handlerForTesting);

        JUnitUploadResult result;
        try
        {
            result = await client.UploadJUnitReportAsync(xml, options.Release, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            output.WriteLine($"upload-junit: upload failed: {ex.Message}");
            return 1;
        }
        catch (TaskCanceledException ex)
        {
            output.WriteLine($"upload-junit: upload timed out: {ex.Message}");
            return 1;
        }

        if (!result.Success)
        {
            // The platform's own words — it names which cap or malformation caused the rejection,
            // which a generic message would throw away.
            output.WriteLine($"upload-junit: rejected by the platform: {result.FailureDetail}");
            return 1;
        }

        output.WriteLine($"Uploaded {options.FilePath}: {result.Recorded} test case(s) recorded.");
        if (!string.IsNullOrWhiteSpace(result.RunUrl))
        {
            output.WriteLine($"Run history: {result.RunUrl}");
        }

        return 0;
    }

    private sealed record Options(string FilePath, string? Release);

    private static (Options? Options, string? Error) ParseOptions(string[] args)
    {
        string? filePath = null;
        string? release = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--release")
            {
                if (i + 1 >= args.Length)
                {
                    return (null, "upload-junit: --release expects a value.");
                }

                release = args[++i];
                continue;
            }

            if (arg.StartsWith('-'))
            {
                return (null, $"upload-junit: unrecognized option '{arg}'.");
            }

            if (filePath is not null)
            {
                return (null, $"upload-junit: unexpected extra argument '{arg}'.");
            }

            filePath = arg;
        }

        return filePath is null
            ? (null, "upload-junit: a path to a JUnit XML file is required.")
            : (new Options(filePath, string.IsNullOrWhiteSpace(release) ? null : release), null);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> environment, string key) =>
        environment.TryGetValue(key, out var value) ? value : null;
}
