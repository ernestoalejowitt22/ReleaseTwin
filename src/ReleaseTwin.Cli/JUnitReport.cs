using System.Xml;
using System.Xml.Linq;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli;

/// <summary>
/// ci-report-formats: shared parent-directory check for the CLI's on-request report files
/// (<c>--summary-json</c>, <c>--junit-xml</c>). Returns a one-line error message for the caller to
/// print, or null when the destination's directory exists.
/// </summary>
internal static class ReportDestination
{
    public static string? Validate(string path, string optionName)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return !string.IsNullOrEmpty(directory) && !Directory.Exists(directory)
            ? $"{optionName}: directory does not exist: {directory}"
            : null;
    }
}

/// <summary>
/// ci-report-formats: maps a run's per-case result onto a JUnit <c>&lt;testcase&gt;</c> state. The
/// mapping is total — every <see cref="FlagProofOutcome"/> and every plain-case outcome resolves to
/// exactly one of "pass" (null message) or "failure" (non-null message). There is no skipped state:
/// a flag-proof case that asked for a paired run and did not get one (<see cref="FlagProofOutcome.Ineligible"/>,
/// <see cref="FlagProofOutcome.ControlFailed"/>, <see cref="FlagProofOutcome.ControlUnverified"/>) is a
/// failure in the report, deliberately stricter than the CLI exit code. See design.md D3.
/// </summary>
public static class JUnitOutcomeMap
{
    /// <summary>The <c>&lt;failure&gt;</c> message for a flag-proof outcome, or null when it is a pass.</summary>
    public static string? FlagProofFailureMessage(FlagProofOutcome outcome) => outcome switch
    {
        FlagProofOutcome.Passed => null,
        FlagProofOutcome.WeakOracle
            or FlagProofOutcome.BothFailed
            or FlagProofOutcome.Inverted
            or FlagProofOutcome.Ineligible
            or FlagProofOutcome.ControlFailed
            or FlagProofOutcome.ControlUnverified => outcome.ToString(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome), outcome, "JUnitOutcomeMap does not classify this FlagProofOutcome"),
    };

    /// <summary>The <c>&lt;failure&gt;</c> message for a plain (non-flag-proof) case, or null when it passed.</summary>
    public static string? PlainFailureMessage(bool passed, string? classification) =>
        passed ? null : (string.IsNullOrWhiteSpace(classification) ? "failed" : classification);

    /// <summary>The <c>&lt;failure&gt;</c> message for a summary row, or null when the row is a pass.</summary>
    public static string? FailureMessage(RunSummaryCase row) =>
        row.FlagProof is { Length: > 0 } flagProof
            ? FlagProofFailureMessage(Enum.Parse<FlagProofOutcome>(flagProof))
            : PlainFailureMessage(row.Outcome == "passed", row.Classification);
}

/// <summary>
/// ci-report-formats: writes a JUnit-XML test report describing a run, projected from the same
/// per-case rows as the JSON run summary. Metadata only — ids, outcomes, classifications, and
/// flag-proof outcome names — never fixture content, bodies, headers, or credential values.
/// </summary>
public static class JUnitReportWriter
{
    private const string SuiteName = "releasetwin";

    public static string? ValidateDestination(string path) => ReportDestination.Validate(path, "--junit-xml");

    public static XDocument Build(RunSummary summary)
    {
        var cases = new List<XElement>(summary.Cases.Count);
        var failures = 0;

        foreach (var row in summary.Cases)
        {
            var testcase = new XElement("testcase",
                new XAttribute("name", row.Id),
                new XAttribute("classname", SuiteName));

            var message = JUnitOutcomeMap.FailureMessage(row);
            if (message is not null)
            {
                failures++;
                testcase.Add(new XElement("failure", new XAttribute("message", message), message));
            }

            cases.Add(testcase);
        }

        var suite = new XElement("testsuite",
            new XAttribute("name", SuiteName),
            new XAttribute("tests", summary.Cases.Count),
            new XAttribute("failures", failures),
            cases);

        var suites = new XElement("testsuites",
            new XAttribute("tests", summary.Cases.Count),
            new XAttribute("failures", failures),
            suite);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), suites);
    }

    public static void Write(string path, RunSummary summary)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, settings);
        Build(summary).Save(writer);
    }
}
