using ReleaseTwin.Adapters.Ui;
using ReleaseTwin.Cli;
using ReleaseTwin.Cli.Evidence.Viewer;

namespace ReleaseTwin.Cli.Tests;

/// <summary>local-evidence-viewer: `releasetwin view` — dispatch, options, export, and video matching.</summary>
public class EvidenceViewerCommandTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = new();

    [Fact]
    public async Task ViewIsMatchedBeforeTheRunFallthrough()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        var exportPath = temp.Combine("evidence.html");
        var output = new StringWriter();

        // CliEntrypoint treats an unrecognized head as a directory of cases to *run*. If `view` were
        // not an explicit branch ahead of that fallthrough, this would attempt a run against the
        // evidence directory and write no report at all.
        var exit = await CliEntrypoint.RunAsync(
            new[] { "view", temp.Path, "--export", exportPath }, NoEnvironment, output);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(exportPath), $"view did not render; it printed: {output}");
        Assert.Contains("CASE-1", File.ReadAllText(exportPath));
    }

    [Fact]
    public async Task ViewReportsAnEmptyDirectoryItselfRatherThanHandingItToTheRunPath()
    {
        using var temp = new TempDirectory();
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(new[] { "view", temp.Path }, NoEnvironment, output);

        Assert.Equal(1, exit);
        Assert.Contains("no case directories", output.ToString());
    }

    [Fact]
    public async Task UsageNamesViewItsFlagsAndTheContainerPortMapping()
    {
        var output = new StringWriter();

        await CliEntrypoint.RunAsync(new[] { "--help" }, NoEnvironment, output);
        var usage = output.ToString();

        Assert.Contains("releasetwin view [dir]", usage);
        Assert.Contains("--export <file.html>", usage);
        Assert.Contains("--video-dir <dir>", usage);
        Assert.Contains("-p 8080:8080", usage);
    }

    [Fact]
    public void DefaultsToTheEvidenceDirectoryEnvironmentVariableThenToEvidence()
    {
        var (fromArg, _) = EvidenceViewerCommand.ParseOptions(new[] { "./some-dir" }, NoEnvironment);
        var (fromEnv, _) = EvidenceViewerCommand.ParseOptions(
            Array.Empty<string>(), new Dictionary<string, string?> { ["RELEASETWIN_EVIDENCE_DIR"] = "/from/env" });
        var (fallback, _) = EvidenceViewerCommand.ParseOptions(Array.Empty<string>(), NoEnvironment);

        Assert.Equal("./some-dir", fromArg.Directory);
        Assert.Equal("/from/env", fromEnv.Directory);
        Assert.Equal("evidence", fallback.Directory);
    }

    [Fact]
    public void TakesTheVideoDirectoryFromTheFlagThenTheRecordersEnvironmentVariable()
    {
        var environment = new Dictionary<string, string?> { ["RELEASETWIN_UI_VIDEO_DIR"] = "/from/env" };

        var (flagWins, _) = EvidenceViewerCommand.ParseOptions(new[] { "--video-dir", "/from/flag" }, environment);
        var (fromEnv, _) = EvidenceViewerCommand.ParseOptions(Array.Empty<string>(), environment);
        var (none, _) = EvidenceViewerCommand.ParseOptions(Array.Empty<string>(), NoEnvironment);

        Assert.Equal("/from/flag", flagWins.VideoDirectory);
        Assert.Equal("/from/env", fromEnv.VideoDirectory);
        Assert.Null(none.VideoDirectory);
    }

    [Theory]
    [InlineData("--export")]
    [InlineData("--video-dir")]
    public void AnOptionWithNoValueIsAnError(string option)
    {
        var (_, error) = EvidenceViewerCommand.ParseOptions(new[] { option }, NoEnvironment);

        Assert.Contains($"{option} expects a value", error);
    }

    [Fact]
    public void AnUnrecognizedOptionIsAnErrorRatherThanAnEvidenceDirectory()
    {
        var (_, error) = EvidenceViewerCommand.ParseOptions(new[] { "--serve-forever" }, NoEnvironment);

        Assert.Contains("Unrecognized option '--serve-forever'", error);
    }

    [Fact]
    public async Task ExportWritesOneSelfContainedFileAndPrintsWhereItWroteIt()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");
        var exportPath = temp.Combine("out", "evidence.html");
        var output = new StringWriter();

        var exit = await EvidenceViewerCommand.RunAsync(
            new[] { temp.Path, "--export", exportPath }, NoEnvironment, output);

        Assert.Equal(0, exit);
        Assert.Contains(Path.GetFullPath(exportPath), output.ToString());

        var html = File.ReadAllText(exportPath);
        Assert.Contains("CASE-1", html);
        Assert.Contains("data:image/png;base64,", html);
        // Self-contained: nothing to fetch, so the original directory can be gone.
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html);
        Assert.DoesNotContain("<script src=", html);
    }

    [Fact]
    public async Task TheExportedFileSurvivesDeletingTheEvidenceDirectory()
    {
        using var outputDir = new TempDirectory();
        var exportPath = outputDir.Combine("evidence.html");

        string html;
        using (var evidence = new TempDirectory())
        {
            evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
            evidence.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");
            await EvidenceViewerCommand.RunAsync(new[] { evidence.Path, "--export", exportPath }, NoEnvironment, new StringWriter());
            html = File.ReadAllText(exportPath);
        }

        Assert.Contains("CASE-1", html);
        Assert.Contains("data:image/png;base64,", html);
    }

    [Fact]
    public async Task AMissingDirectoryExitsNonZeroWithoutWritingAnything()
    {
        using var temp = new TempDirectory();
        var exportPath = temp.Combine("evidence.html");
        var output = new StringWriter();

        var exit = await EvidenceViewerCommand.RunAsync(
            new[] { temp.Combine("absent"), "--export", exportPath }, NoEnvironment, output);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(exportPath));
        Assert.Contains("No evidence directory at", output.ToString());
    }

    [Fact]
    public void MatchesACaseToTheRecordingTheRecorderWouldHaveWritten()
    {
        using var evidence = new TempDirectory();
        using var videos = new TempDirectory();
        evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        File.WriteAllBytes(videos.Combine(SessionRecordingFile.Sanitize("CASE-1") + ".webm"), new byte[] { 1, 2, 3 });

        var attached = EvidenceViewerCommand.AttachVideos(
            EvidenceDirectoryReader.Read(evidence.Path).Directory!, videos.Path);

        Assert.NotNull(attached.Cases.Single().VideoPath);
    }

    [Fact]
    public void ACaseWithNoRecordingRendersAsIfVideoWasNeverEnabled()
    {
        using var evidence = new TempDirectory();
        using var videos = new TempDirectory();
        evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        var directory = EvidenceDirectoryReader.Read(evidence.Path).Directory!;

        var withEmptyVideoDir = EvidenceReportRenderer.Render(
            EvidenceViewerCommand.AttachVideos(directory, videos.Path), new ServedAssetLinks());
        var withNoVideoDir = EvidenceReportRenderer.Render(
            EvidenceViewerCommand.AttachVideos(directory, null), new ServedAssetLinks());

        Assert.Equal(withNoVideoDir, withEmptyVideoDir);
        Assert.DoesNotContain("<video", withEmptyVideoDir);
        Assert.DoesNotContain("rt-video", withEmptyVideoDir);
    }

    [Fact]
    public void AMatchedRecordingPlaysInlineWithThatCase()
    {
        using var evidence = new TempDirectory();
        using var videos = new TempDirectory();
        evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        File.WriteAllBytes(videos.Combine("CASE-1.webm"), new byte[] { 1, 2, 3 });

        var directory = EvidenceViewerCommand.AttachVideos(EvidenceDirectoryReader.Read(evidence.Path).Directory!, videos.Path);

        var served = EvidenceReportRenderer.Render(directory, new ServedAssetLinks());
        Assert.Contains("<video controls preload=\"metadata\" src=\"/case/CASE-1/video.webm\">", served);

        var exported = EvidenceReportRenderer.Render(directory, new InlinedAssetLinks());
        Assert.Contains("src=\"data:video/webm;base64,", exported);
    }

    [Fact]
    public void AnUnreadableRecordingDoesNotStopTheRestOfTheCaseRendering()
    {
        using var evidence = new TempDirectory();
        evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        var directory = EvidenceDirectoryReader.Read(evidence.Path).Directory!;
        var missing = directory with
        {
            Cases = new[] { directory.Cases[0] with { VideoPath = evidence.Combine("gone.webm") } },
        };

        var html = EvidenceReportRenderer.Render(missing, new InlinedAssetLinks());

        Assert.Contains("could not be read, so it is not in this file", html);
        Assert.Contains("$.total", html);
        Assert.Contains("Redacted by your CLI", html);
    }

    [Fact]
    public void AnOversizedRecordingIsOmittedFromTheExportAndSaidSo()
    {
        using var evidence = new TempDirectory();
        evidence.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        var videoPath = evidence.Combine("big.webm");
        using (var file = File.Create(videoPath))
        {
            file.SetLength(InlinedAssetLinks.VideoByteCap + 1);
        }

        var directory = EvidenceDirectoryReader.Read(evidence.Path).Directory!;
        var withBigVideo = directory with { Cases = new[] { directory.Cases[0] with { VideoPath = videoPath } } };

        var exported = EvidenceReportRenderer.Render(withBigVideo, new InlinedAssetLinks());
        Assert.Contains("Session recording omitted from this file", exported);
        Assert.Contains(videoPath, exported);
        Assert.DoesNotContain("data:video/webm", exported);

        // The served view streams from disk, so it has no such limit.
        Assert.Contains("/case/CASE-1/video.webm", EvidenceReportRenderer.Render(withBigVideo, new ServedAssetLinks()));
    }

    [Fact]
    public void DetectsAContainerFromTheEnvironmentVariableTheBaseImagesSet()
    {
        Assert.True(EvidenceViewerCommand.IsContainerized(new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = "true" }));
        Assert.True(EvidenceViewerCommand.IsContainerized(new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = "1" }));
    }
}
