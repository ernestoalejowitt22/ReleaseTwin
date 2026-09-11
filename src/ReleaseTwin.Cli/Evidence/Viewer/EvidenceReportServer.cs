using System.Net;
using System.Text;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>
/// local-evidence-viewer (design D2): serves one rendered report over
/// <see cref="HttpListener"/> — in the base framework, so <c>dotnet tool install -g releasetwin</c>
/// keeps working for users who have only the .NET runtime and not the ASP.NET Core shared one.
///
/// This is a report server, not a file server: screenshots and recordings are looked up by case
/// directory name and screenshot id in the already-read model, never by joining a request path onto
/// a filesystem path, so a crafted URL cannot reach outside the evidence directory.
/// </summary>
public sealed class EvidenceReportServer : IDisposable
{
    public const int DefaultPort = 8080;

    /// <summary>How many consecutive ports to try before giving up, when the preferred one is taken.</summary>
    private const int PortAttempts = 20;

    private readonly HttpListener _listener;
    private readonly EvidenceDirectoryView _directory;
    private readonly string _html;

    private EvidenceReportServer(HttpListener listener, EvidenceDirectoryView directory, string html, int port, string url)
    {
        _listener = listener;
        _directory = directory;
        _html = html;
        Port = port;
        Url = url;
    }

    public int Port { get; }

    /// <summary>The URL to open. Always printed — the report is reachable by URL, never by assuming a browser.</summary>
    public string Url { get; }

    /// <summary>The prefixes actually bound, so a test can assert what this is exposed on.</summary>
    public IReadOnlyList<string> Prefixes => _listener.Prefixes.ToList();

    /// <summary>
    /// Binds loopback normally and all interfaces when containerized (design D6): inside a container
    /// loopback is unreachable from the host even with <c>-p</c>, and the network exposure there is
    /// already governed by whether the operator published the port.
    /// </summary>
    public static EvidenceReportServer Start(EvidenceDirectoryView directory, string html, bool bindAllInterfaces, int preferredPort = DefaultPort)
    {
        HttpListenerException? last = null;

        for (var port = preferredPort; port < preferredPort + PortAttempts; port++)
        {
            var listener = new HttpListener();
            foreach (var prefix in PrefixesFor(port, bindAllInterfaces))
            {
                listener.Prefixes.Add(prefix);
            }

            try
            {
                listener.Start();
                return new EvidenceReportServer(listener, directory, html, port, $"http://localhost:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}/");
            }
            catch (HttpListenerException ex)
            {
                // Port in use, or (on Windows) the "+" prefix needs an ACL. Either way, try the next.
                last = ex;
                listener.Close();
            }
        }

        throw new IOException(
            $"Could not bind a port for the evidence viewer between {preferredPort} and {preferredPort + PortAttempts - 1}.", last);
    }

    /// <summary>
    /// A single <c>http://localhost:{port}/</c> prefix binds only the *first* address localhost
    /// resolves to. On a dual-stack machine that is <c>[::1]</c>, so <c>http://127.0.0.1:{port}/</c>
    /// refuses connections outright — measured, not theorized. Registering <c>127.0.0.1</c>
    /// alongside <c>localhost</c> makes both families answer.
    ///
    /// An explicit <c>http://[::1]:{port}/</c> prefix is not an option: HttpListener rejects the
    /// bracketed IPv6 literal with "Invalid port in prefix". <c>localhost</c> is what covers IPv6.
    /// </summary>
    private static IEnumerable<string> PrefixesFor(int port, bool bindAllInterfaces)
    {
        var p = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (bindAllInterfaces)
        {
            yield return $"http://+:{p}/";
            yield break;
        }

        yield return $"http://127.0.0.1:{p}/";
        yield return $"http://localhost:{p}/";
    }

    /// <summary>Serves until <paramref name="cancellationToken"/> is signalled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() =>
        {
            // Stop() makes the pending GetContextAsync() throw, which is how the loop exits.
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            try
            {
                Handle(context);
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
                // A client that went away mid-response is not a reason to stop serving.
            }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (path is "/" or "/index.html")
        {
            Respond(context, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(_html));
            return;
        }

        if (path == ServedAssetLinks.StylesheetPath)
        {
            Respond(context, 200, "text/css; charset=utf-8", Encoding.UTF8.GetBytes(ViewerAssets.Css));
            return;
        }

        if (path == ServedAssetLinks.ScriptPath)
        {
            Respond(context, 200, "text/javascript; charset=utf-8", Encoding.UTF8.GetBytes(ViewerAssets.Js));
            return;
        }

        // /case/<case-directory>/screenshot/<id>.png  and  /case/<case-directory>/video.webm
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && segments[0] == "case")
        {
            var directoryName = Uri.UnescapeDataString(segments[1]);
            var evidenceCase = _directory.Cases.FirstOrDefault(c => string.Equals(c.DirectoryName, directoryName, StringComparison.Ordinal));
            if (evidenceCase is not null)
            {
                if (segments.Length == 4 && segments[2] == "screenshot")
                {
                    var id = Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(segments[3]));
                    var file = evidenceCase.Screenshots.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
                    if (file is not null)
                    {
                        RespondFile(context, "image/png", file.Path);
                        return;
                    }
                }
                else if (segments.Length == 3 && segments[2] == "video.webm" && evidenceCase.VideoPath is { } videoPath)
                {
                    RespondFile(context, "video/webm", videoPath);
                    return;
                }
            }
        }

        Respond(context, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"));
    }

    private static void RespondFile(HttpListenerContext context, string contentType, string filePath)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Respond(context, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not readable"));
            return;
        }

        Respond(context, 200, contentType, bytes);
    }

    private static void Respond(HttpListenerContext context, int status, string contentType, byte[] body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = body.Length;
        context.Response.OutputStream.Write(body, 0, body.Length);
        context.Response.Close();
    }

    public void Dispose() => _listener.Close();
}
