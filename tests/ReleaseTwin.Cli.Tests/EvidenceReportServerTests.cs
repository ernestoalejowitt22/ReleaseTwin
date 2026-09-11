using ReleaseTwin.Cli.Evidence.Viewer;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// local-evidence-viewer: the serving shell. Rendering is covered without a socket
/// (<see cref="EvidenceReportRendererTests"/>); these exercise the parts only a real listener can —
/// that the report, its embedded assets, and its screenshots actually come back over HTTP, and that
/// a crafted URL cannot reach outside the evidence directory.
/// </summary>
public class EvidenceReportServerTests : IDisposable
{
    private readonly TempDirectory _evidence = new();
    private readonly EvidenceDirectoryView _directory;

    public EvidenceReportServerTests()
    {
        _evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        _evidence.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");
        _directory = EvidenceDirectoryReader.Read(_evidence.Path).Directory!;
    }

    [Fact]
    public async Task ServesTheReportItsAssetsAndItsScreenshots()
    {
        var html = EvidenceReportRenderer.Render(_directory, new ServedAssetLinks());
        using var server = EvidenceReportServer.Start(_directory, html, bindAllInterfaces: false, preferredPort: FreePort());
        using var cancellation = new CancellationTokenSource();
        var serving = server.RunAsync(cancellation.Token);
        using var client = new HttpClient { BaseAddress = new Uri(server.Url) };

        var report = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, report.StatusCode);
        Assert.Contains("tickets/CASE-1", await report.Content.ReadAsStringAsync());

        Assert.Contains(".rt-step {", await client.GetStringAsync(ServedAssetLinks.StylesheetPath));
        Assert.Contains("rt-nav", await client.GetStringAsync(ServedAssetLinks.ScriptPath));

        var screenshot = await client.GetAsync("/case/CASE-1/screenshot/3f1a9c2e4b5d6f708192a3b4c5d6e7f8.png");
        Assert.Equal(System.Net.HttpStatusCode.OK, screenshot.StatusCode);
        Assert.Equal("image/png", screenshot.Content.Headers.ContentType?.MediaType);
        Assert.Equal(EvidenceViewerFixtures.OnePixelPng, await screenshot.Content.ReadAsByteArrayAsync());

        await cancellation.CancelAsync();
        await serving;
    }

    [Fact]
    public async Task DoesNotServeFilesOutsideTheEvidenceDirectory()
    {
        using var server = EvidenceReportServer.Start(_directory, "<html></html>", bindAllInterfaces: false, preferredPort: FreePort());
        using var cancellation = new CancellationTokenSource();
        var serving = server.RunAsync(cancellation.Token);
        using var client = new HttpClient { BaseAddress = new Uri(server.Url) };

        // Screenshots are looked up by case name and id in the already-read model, never by joining a
        // request path onto a filesystem path — so there is no path to traverse.
        foreach (var path in new[]
                 {
                     "/case/CASE-1/screenshot/..%2F..%2F..%2Fetc%2Fpasswd.png",
                     "/case/..%2F..%2Fetc/screenshot/passwd.png",
                     "/etc/passwd",
                     "/case/CASE-1/video.webm",
                 })
        {
            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        }

        await cancellation.CancelAsync();
        await serving;
    }

    [Fact]
    public void TakesTheNextFreePortWhenThePreferredOneIsInUse()
    {
        var port = FreePort();
        using var holder = EvidenceReportServer.Start(_directory, "<html></html>", bindAllInterfaces: false, preferredPort: port);

        using var second = EvidenceReportServer.Start(_directory, "<html></html>", bindAllInterfaces: false, preferredPort: port);

        Assert.NotEqual(holder.Port, second.Port);
        Assert.Contains(second.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), second.Url);
    }

    /// <summary>A port the OS just handed out and released — far likelier to be free than a fixed one.</summary>
    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose() => _evidence.Dispose();
}
