using ReleaseTwin.Cli.Evidence.Viewer;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// local-evidence-viewer: every rendering requirement, asserted against the rendered string. No
/// socket, no port, no browser — that is what design D1's split between rendering and serving buys.
/// </summary>
public class EvidenceReportRendererTests
{
    [Fact]
    public void RendersTheOrdinaryDocumentsIdentityAndEveryStepsValues()
    {
        var html = RenderOrdinary();

        Assert.Contains("CASE-1", html);
        Assert.Contains("tickets/CASE-1", html);
        Assert.Contains("Redacted by your CLI before upload. Screenshots are best-effort-redacted.", html);

        // Step 0: the adapter-evidence step.
        Assert.Contains("http.request", html);
        Assert.Contains("142 ms", html);
        Assert.Contains("Adapter evidence", html);
        Assert.Contains("api.example.test/orders", html);

        // Step 1: a passing assertion, expression / expected / observed.
        Assert.Contains("$.status", html);
        Assert.Contains("confirmed", html);

        // Step 3: the failing assertion — the reason the run failed.
        Assert.Contains("$.total", html);
        Assert.Contains("49.99", html);
        Assert.Contains("0.00", html);
        Assert.Contains("rt-outcome-Failed", html);

        // Step 4: never executed, and still rendered with its index, operation, outcome, duration.
        Assert.Contains("rt-outcome-NotExecuted", html);
        Assert.Contains("#4", html);
        Assert.Contains("0 ms", html);
    }

    [Fact]
    public void RendersEveryStepIndexInPipelineOrder()
    {
        var html = RenderOrdinary();

        var indexes = new[] { "#0", "#1", "#2", "#3", "#4" }.Select(i => html.IndexOf(i, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(-1, indexes);
        Assert.Equal(indexes.OrderBy(i => i), indexes);
    }

    [Fact]
    public void RendersNamedLegsAsDistinctLabelledSections()
    {
        var html = RenderFlagProof();

        Assert.Contains("<h3>known-bad</h3>", html);
        Assert.Contains("<h3>known-good</h3>", html);
        Assert.True(
            html.IndexOf("known-bad", StringComparison.Ordinal) < html.IndexOf("known-good", StringComparison.Ordinal),
            "the legs render in document order");
    }

    [Fact]
    public void RendersASingleUnnamedLegAsOneUnlabelledSequence()
    {
        var html = RenderOrdinary();

        // One leg section, and no leg heading at all — an ordinary case is not "a leg".
        Assert.Equal(1, Occurrences(html, "class=\"rt-leg\""));
        Assert.DoesNotContain("<h3>", html);
    }

    [Fact]
    public void RendersAStepThatOmitsEveryOptionalFieldWithoutError()
    {
        var html = RenderFlagProof();

        // known-good step 0 carries no assertion, no adapter, no screenshots.
        Assert.Contains("No assertion, adapter evidence, or screenshot recorded for this step.", html);
        Assert.Contains("88 ms", html);
    }

    [Fact]
    public void LabelsScreenshotsAsBestEffortRedacted()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");

        var html = EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new ServedAssetLinks());

        Assert.Contains("best-effort redacted by the CLI that produced it", html);
        Assert.Contains("/case/CASE-1/screenshot/3f1a9c2e4b5d6f708192a3b4c5d6e7f8.png", html);
    }

    [Fact]
    public void SaysSoWhenAReferencedScreenshotFileIsNotOnDisk()
    {
        var html = RenderOrdinary();

        Assert.Contains("is referenced by this step but its file is not in the case directory", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void ListsFailedCasesFirstInTheNavigation()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("AAA-PASSING", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.FlagProofFileName));
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));

        var html = EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new ServedAssetLinks());
        var nav = html[html.IndexOf("<nav", StringComparison.Ordinal)..html.IndexOf("</nav>", StringComparison.Ordinal)];

        Assert.True(
            nav.IndexOf("CASE-1", StringComparison.Ordinal) < nav.IndexOf("FP-EV", StringComparison.Ordinal),
            "the failed case is listed first");
        Assert.Contains("rt-badge-failed", nav);
        Assert.Contains("rt-badge-passed", nav);
    }

    [Fact]
    public void RendersACaseThatFailedToLoadWithItsReasonRatherThanOmittingIt()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-BROKEN", "{ not json");

        var html = EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new ServedAssetLinks());

        Assert.Contains("CASE-BROKEN", html);
        Assert.Contains("could not be read", html);
    }

    [Fact]
    public void EscapesEverythingItTakesFromDisk()
    {
        using var temp = new TempDirectory();
        temp.WriteCase(
            "CASE-X",
            EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName)
                .Replace("tickets/CASE-1", "<script>alert(1)</script>"));

        var html = EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new ServedAssetLinks());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Fact]
    public void ServedAndExportedReportsDifferOnlyInHowAssetsAreReferenced()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        var directory = EvidenceDirectoryReader.Read(temp.Path).Directory!;

        var served = EvidenceReportRenderer.Render(directory, new ServedAssetLinks());
        var exported = EvidenceReportRenderer.Render(directory, new InlinedAssetLinks());

        // Same renderer, same evidence: the export links its stylesheet inline, the served view fetches it.
        Assert.Contains("<link rel=\"stylesheet\"", served);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", exported);
        Assert.Contains(".rt-step {", exported);
        foreach (var marker in new[] { "CASE-1", "tickets/CASE-1", "$.total", "49.99", "rt-outcome-NotExecuted" })
        {
            Assert.Contains(marker, served);
            Assert.Contains(marker, exported);
        }
    }

    [Fact]
    public void TheExportEmbedsScreenshotsAsDataUris()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");

        var html = EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new InlinedAssetLinks());

        Assert.Contains("src=\"data:image/png;base64,", html);
        Assert.DoesNotContain("/case/CASE-1/screenshot/", html);
    }

    private static string RenderOrdinary() => RenderFixture(EvidenceViewerFixtures.OrdinaryFileName, "CASE-1");

    private static string RenderFlagProof() => RenderFixture(EvidenceViewerFixtures.FlagProofFileName, "FP-EV");

    private static string RenderFixture(string fixtureFileName, string caseDirectoryName)
    {
        using var temp = new TempDirectory();
        temp.WriteCase(caseDirectoryName, EvidenceViewerFixtures.Read(fixtureFileName));
        return EvidenceReportRenderer.Render(EvidenceDirectoryReader.Read(temp.Path).Directory!, new ServedAssetLinks());
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
