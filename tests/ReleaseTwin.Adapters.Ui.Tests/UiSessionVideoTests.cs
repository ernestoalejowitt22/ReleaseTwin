using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui.Tests;

/// <summary>
/// ui-session-video: its own class (not `UiAdapterTests`) so the shared per-test `UiAdapter` that
/// class creates doesn't run a competing Playwright instance alongside the video-recording one —
/// in production the CLI only ever constructs a single `UiAdapter`.
/// </summary>
public class UiSessionVideoTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();

    private static TestCase BuildCase(string caseId, StaticPageServer server) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        new FixtureReference("fixtures/case.json", FixtureHash, FixtureContent),
        Array.Empty<PrerequisiteDeclaration>(),
        new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = server.Url }),
            new PipelineStep("ui.assertVisible", With: new Dictionary<string, object?> { ["selector"] = "#greeting" }),
            // Keep the session on-screen long enough for Playwright to flush real video frames — a
            // sub-second run can close the context before any frame is written.
            new PipelineStep("ui.waitFor", With: new Dictionary<string, object?> { ["selector"] = "#delayed", ["state"] = "visible", ["timeoutMs"] = 5000 }),
        },
        new[] { new CleanupDeclaration("ui.closePage") });

    [Fact]
    public async Task RecordVideoDir_ProducesANamedWebmForTheRun()
    {
        using var server = new StaticPageServer();
        var videoDir = Directory.CreateTempSubdirectory("ui-video-").FullName;
        var adapter = await UiAdapter.CreateAsync(recordVideoDir: videoDir);
        try
        {
            var root = new CompositionRoot();
            root.Install(adapter);
            var report = await root.BuildExecutor().ExecuteAsync(BuildCase("UI-VIDEO-1", server));

            Assert.True(report.Passed, report.FailureDetail);

            var webm = Path.Combine(videoDir, "UI-VIDEO-1.webm");
            var contents = string.Join(", ", Directory.GetFiles(videoDir).Select(Path.GetFileName));
            Assert.True(File.Exists(webm), $"expected {webm}; dir has: [{contents}]");
            Assert.True(new FileInfo(webm).Length > 0);
        }
        finally
        {
            adapter.Dispose();
            Directory.Delete(videoDir, recursive: true);
        }
    }

    // The no-recordVideoDir path (context created without any RecordVideo* option, no files written)
    // is already covered by every test in UiAdapterTests — none of them pass a video dir and none
    // produce a video file. A second UiAdapter here would just be a competing Playwright instance.
}
