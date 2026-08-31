using ReleaseTwin.Cli;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-pr-integration: the run-summary builder/writer shape, tallies, and destination validation.</summary>
public class RunSummaryTests
{
    [Fact]
    public void BuildProducesTheVersionedShapeWithLowercasedOutcomes()
    {
        var b = new RunSummaryBuilder();
        b.AddCase("A", passed: true, classification: null, flagProofOutcome: null, release: "4.2");
        b.AddCase("B", passed: false, classification: "Infrastructure", flagProofOutcome: null, release: null);

        var summary = b.Build();

        Assert.Equal(1, summary.SchemaVersion);
        Assert.Equal("failed", summary.Overall);
        Assert.Equal(1, summary.Totals.Passed);
        Assert.Equal(1, summary.Totals.Failed);
        Assert.Equal(2, summary.Totals.Cases);
        Assert.Equal("infrastructure", summary.Cases[1].Classification);
        Assert.Equal("passed", summary.Cases[0].Outcome);
        Assert.Equal("4.2", summary.Cases[0].Release);
        Assert.Null(summary.Cases[1].Release);
    }

    [Fact]
    public void FlagProofTalliesSplitProvenIneligibleAndRegressed()
    {
        var b = new RunSummaryBuilder();
        b.AddCase("p", passed: true, classification: null, flagProofOutcome: "Passed", release: null);
        b.AddCase("i", passed: false, classification: null, flagProofOutcome: "Ineligible", release: null);
        b.AddCase("r1", passed: false, classification: null, flagProofOutcome: "WeakOracle", release: null);
        b.AddCase("r2", passed: false, classification: null, flagProofOutcome: "Inverted", release: null);
        b.AddCase("plain", passed: true, classification: null, flagProofOutcome: null, release: null);

        var fp = b.Build().FlagProof;

        Assert.Equal(1, fp.Proven);
        Assert.Equal(1, fp.Ineligible);
        Assert.Equal(2, fp.Regressed);
    }

    [Fact]
    public void AllPassingYieldsOverallPassed()
    {
        var b = new RunSummaryBuilder();
        b.AddCase("A", passed: true, classification: null, flagProofOutcome: null, release: null);
        Assert.Equal("passed", b.Build().Overall);
    }

    [Fact]
    public void ValidateDestinationRejectsAMissingDirectory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "releasetwin-nope-" + Guid.NewGuid(), "out.json");
        Assert.NotNull(RunSummaryWriter.ValidateDestination(missing));
    }

    [Fact]
    public void ValidateDestinationAcceptsAnExistingDirectory()
    {
        var dir = Directory.CreateTempSubdirectory("releasetwin-summary-ok-").FullName;
        Assert.Null(RunSummaryWriter.ValidateDestination(Path.Combine(dir, "out.json")));
    }

    [Fact]
    public void WriteEmitsIndentedJsonWithATrailingNewline()
    {
        var dir = Directory.CreateTempSubdirectory("releasetwin-summary-write-").FullName;
        var path = Path.Combine(dir, "out.json");
        var b = new RunSummaryBuilder();
        b.AddCase("A", passed: true, classification: null, flagProofOutcome: null, release: null);

        RunSummaryWriter.Write(path, b.Build());

        var text = File.ReadAllText(path);
        Assert.Contains("\"schemaVersion\": 1", text);
        Assert.EndsWith("\n", text);
    }
}
