using System.Xml.Linq;
using ReleaseTwin.Cli;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Tests;

/// <summary>ci-report-formats: the outcome→JUnit mapping totality, the emitted XML shape, escaping, and destination validation.</summary>
public class JUnitReportTests
{
    [Fact]
    public void FlagProofMappingIsTotalOverTheEnum()
    {
        // A new FlagProofOutcome value with no branch throws here — the mapping must stay total (design.md D3).
        foreach (var outcome in Enum.GetValues<FlagProofOutcome>())
        {
            var message = JUnitOutcomeMap.FlagProofFailureMessage(outcome);
            if (outcome == FlagProofOutcome.Passed)
            {
                Assert.Null(message);
            }
            else
            {
                Assert.Equal(outcome.ToString(), message);
            }
        }
    }

    [Fact]
    public void IneligibleControlFailedAndControlUnverifiedAreFailuresNotSkips()
    {
        Assert.Equal("Ineligible", JUnitOutcomeMap.FlagProofFailureMessage(FlagProofOutcome.Ineligible));
        Assert.Equal("ControlFailed", JUnitOutcomeMap.FlagProofFailureMessage(FlagProofOutcome.ControlFailed));
        Assert.Equal("ControlUnverified", JUnitOutcomeMap.FlagProofFailureMessage(FlagProofOutcome.ControlUnverified));
    }

    [Fact]
    public void PlainFailureMessageFallsBackToFailedWhenUnclassified()
    {
        Assert.Null(JUnitOutcomeMap.PlainFailureMessage(passed: true, classification: null));
        Assert.Equal("failed", JUnitOutcomeMap.PlainFailureMessage(passed: false, classification: null));
        Assert.Equal("infrastructure", JUnitOutcomeMap.PlainFailureMessage(passed: false, classification: "infrastructure"));
    }

    private static RunSummary SummaryOf(params (string Id, bool Passed, string? Classification, string? FlagProof)[] rows)
    {
        var b = new RunSummaryBuilder();
        foreach (var (id, passed, classification, flagProof) in rows)
        {
            b.AddCase(id, passed, classification, flagProof, release: null);
        }

        return b.Build();
    }

    [Fact]
    public void ReportParsesAndSuiteCountsMatchTheCases()
    {
        var doc = JUnitReportWriter.Build(SummaryOf(
            ("A", true, null, null),
            ("B", false, "infrastructure", null),
            ("C", true, null, "Passed"),
            ("D", false, null, "WeakOracle"),
            ("E", false, null, "Ineligible")));

        var suites = doc.Root!;
        Assert.Equal("testsuites", suites.Name.LocalName);
        Assert.Equal("5", suites.Attribute("tests")!.Value);
        Assert.Equal("3", suites.Attribute("failures")!.Value);

        var suite = suites.Element("testsuite")!;
        Assert.Equal("5", suite.Attribute("tests")!.Value);
        Assert.Equal("3", suite.Attribute("failures")!.Value);

        Assert.Equal(5, suite.Elements("testcase").Count());
    }

    [Fact]
    public void PassingCasesHaveNoChildAndNeverEmitsSkipped()
    {
        var doc = JUnitReportWriter.Build(SummaryOf(
            ("A", true, null, null),
            ("C", true, null, "Passed"),
            ("E", false, null, "Ineligible"),
            ("F", false, null, "ControlFailed"),
            ("G", false, null, "ControlUnverified")));

        var cases = doc.Descendants("testcase").ToList();
        Assert.Empty(cases[0].Elements());
        Assert.Empty(cases[1].Elements());
        Assert.Equal("Ineligible", cases[2].Element("failure")!.Attribute("message")!.Value);

        Assert.Empty(doc.Descendants("skipped"));
    }

    [Fact]
    public void SpecialCharactersInIdsAndMessagesAreEscaped()
    {
        var doc = JUnitReportWriter.Build(SummaryOf(
            ("weird & <id> \"x\"", false, "needs <review> & \"care\"", null)));

        var raw = doc.ToString();
        Assert.Contains("&amp;", raw);
        Assert.Contains("&lt;id&gt;", raw);
        // Round-trips: re-parsing yields the original text.
        var reparsed = XDocument.Parse(raw);
        var testcase = reparsed.Descendants("testcase").Single();
        Assert.Equal("weird & <id> \"x\"", testcase.Attribute("name")!.Value);
        Assert.Equal("needs <review> & \"care\"", testcase.Element("failure")!.Attribute("message")!.Value);
    }

    [Fact]
    public void ValidateDestinationRejectsAMissingDirectory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "releasetwin-nope-" + Guid.NewGuid(), "junit.xml");
        var error = JUnitReportWriter.ValidateDestination(missing);
        Assert.NotNull(error);
        Assert.Contains("--junit-xml", error);
    }

    [Fact]
    public void WriteEmitsParseableXmlToDisk()
    {
        var dir = Directory.CreateTempSubdirectory("releasetwin-junit-write-").FullName;
        var path = Path.Combine(dir, "junit.xml");

        JUnitReportWriter.Write(path, SummaryOf(("A", true, null, null)));

        var doc = XDocument.Load(path);
        Assert.Equal("testsuites", doc.Root!.Name.LocalName);
    }
}
