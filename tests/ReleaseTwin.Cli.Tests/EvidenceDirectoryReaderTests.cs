using ReleaseTwin.Cli.Evidence.Viewer;

namespace ReleaseTwin.Cli.Tests;

/// <summary>local-evidence-viewer: reading the on-disk evidence layout without modifying it.</summary>
public class EvidenceDirectoryReaderTests
{
    [Fact]
    public void ReadsACaseDirectoryIntoTheEvidenceDocumentRecords()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");

        var result = EvidenceDirectoryReader.Read(temp.Path);

        Assert.Null(result.Error);
        var evidenceCase = Assert.Single(result.Directory!.Cases);
        Assert.Equal("CASE-1", evidenceCase.CaseId);
        Assert.Equal("tickets/CASE-1", evidenceCase.Document!.OracleLocator);
        Assert.Equal(5, evidenceCase.Document.Legs.Single().Steps.Count);
        Assert.Equal("3f1a9c2e4b5d6f708192a3b4c5d6e7f8", Assert.Single(evidenceCase.Screenshots).Id);
    }

    [Fact]
    public void TreatsOmittedOptionalKeysAsAbsentRatherThanFailing()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));

        var steps = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases.Single().Document!.Legs.Single().Steps;

        // The writer omits null-valued keys entirely; they must land as null, not as a parse failure.
        Assert.Null(steps[0].Assertion);
        Assert.Null(steps[0].Screenshots);
        Assert.Null(steps[1].Adapter);
        var bare = steps.Single(s => s.Outcome == "NotExecuted");
        Assert.Null(bare.Assertion);
        Assert.Null(bare.Adapter);
        Assert.Null(bare.Screenshots);
    }

    [Fact]
    public void ReadsAnUnnamedLegAndNamedLegsAlike()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteCase("FP-EV", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.FlagProofFileName));

        var cases = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases;

        Assert.Null(cases.Single(c => c.CaseId == "CASE-1").Document!.Legs.Single().Leg);
        Assert.Equal(
            new[] { "known-bad", "known-good" },
            cases.Single(c => c.CaseId == "FP-EV").Document!.Legs.Select(l => l.Leg));
    }

    [Fact]
    public void ReportsAMalformedDocumentAsThatCaseFailingToLoad()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteCase("CASE-BROKEN", "{ this is not json");

        var cases = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases;

        // Reported, not crashed — and not silently dropped, because the directory existing is itself
        // evidence that the case ran.
        Assert.Equal(2, cases.Count);
        var broken = cases.Single(c => c.CaseId == "CASE-BROKEN");
        Assert.Null(broken.Document);
        Assert.Contains("could not be read", broken.LoadError);
    }

    [Fact]
    public void ReportsADocumentMissingItsIdentityFieldsAsFailingToLoad()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", """{ "oracleLocator": "tickets/CASE-1" }""");

        var evidenceCase = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases.Single();

        Assert.Null(evidenceCase.Document);
        Assert.Contains("no caseId", evidenceCase.LoadError);
    }

    [Fact]
    public void OrdersCasesWithAFailedStepFirst()
    {
        using var temp = new TempDirectory();
        // "AAA-PASSING" sorts first by name, so only failed-first ordering can put CASE-1 ahead of it.
        temp.WriteCase("AAA-PASSING", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.FlagProofFileName));
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));

        var cases = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases;

        Assert.Equal(new[] { "CASE-1", "FP-EV" }, cases.Select(c => c.CaseId));
        Assert.True(cases[0].HasFailedStep);
        Assert.False(cases[1].HasFailedStep);
    }

    [Fact]
    public void KeepsNameOrderWithinTheFailedAndPassedGroups()
    {
        using var temp = new TempDirectory();
        var passing = EvidenceViewerFixtures.Read(EvidenceViewerFixtures.FlagProofFileName);
        temp.WriteCase("B-PASS", passing.Replace("FP-EV", "B-PASS"));
        temp.WriteCase("A-PASS", passing.Replace("FP-EV", "A-PASS"));
        var failing = EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName);
        temp.WriteCase("Z-FAIL", failing.Replace("CASE-1", "Z-FAIL"));
        temp.WriteCase("M-FAIL", failing.Replace("CASE-1", "M-FAIL"));

        var cases = EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases;

        Assert.Equal(new[] { "M-FAIL", "Z-FAIL", "A-PASS", "B-PASS" }, cases.Select(c => c.CaseId));
    }

    [Fact]
    public void AnExpectedFailureDoesNotMakeACaseSortAsFailed()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("FP-EV", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.FlagProofFileName));

        // The flag-proof fixture's known-bad leg carries an ExpectedFailure — a pass in effect.
        Assert.False(EvidenceDirectoryReader.Read(temp.Path).Directory!.Cases.Single().HasFailedStep);
    }

    [Fact]
    public void AMissingPathIsAnErrorNotAnEmptyReport()
    {
        var result = EvidenceDirectoryReader.Read(Path.Combine(Path.GetTempPath(), "rt-viewer-absent-" + Guid.NewGuid().ToString("n")));

        Assert.Null(result.Directory);
        Assert.Contains("No evidence directory at", result.Error);
    }

    [Fact]
    public void ADirectoryWithNoCaseSubdirectoriesIsAnErrorNotAnEmptyReport()
    {
        using var temp = new TempDirectory();

        var result = EvidenceDirectoryReader.Read(temp.Path);

        Assert.Null(result.Directory);
        Assert.Contains("no case directories", result.Error);
    }

    [Fact]
    public void SubdirectoriesWithNoEvidenceDocumentAreNotCases()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Combine("not-a-case"));

        var result = EvidenceDirectoryReader.Read(temp.Path);

        Assert.Null(result.Directory);
        Assert.Contains("none of them holds an evidence.json", result.Error);
    }

    [Fact]
    public void ReadingDoesNotModifyTheEvidenceDirectory()
    {
        using var temp = new TempDirectory();
        temp.WriteCase("CASE-1", EvidenceViewerFixtures.Read(EvidenceViewerFixtures.OrdinaryFileName));
        temp.WriteScreenshot("CASE-1", "3f1a9c2e4b5d6f708192a3b4c5d6e7f8");
        var before = Snapshot(temp.Path);

        EvidenceDirectoryReader.Read(temp.Path);

        Assert.Equal(before, Snapshot(temp.Path));
    }

    private static string Snapshot(string root) =>
        string.Join(
            "\n",
            Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => File.Exists(p)
                    ? $"{Path.GetRelativePath(root, p)}:{Convert.ToBase64String(File.ReadAllBytes(p))}"
                    : $"{Path.GetRelativePath(root, p)}:dir"));
}
