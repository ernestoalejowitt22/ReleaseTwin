using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReleaseTwin.Cli;

/// <summary>
/// ci-pr-integration: the machine-readable run summary written when <c>--summary-json</c> (or
/// <c>RELEASETWIN_SUMMARY_JSON</c>) is set. Metadata only — ids, outcomes, classifications,
/// flag-proof results, and the <c>release</c> label — never fixture content, response bodies, or
/// credential values. See design.md D-C for the shape.
/// </summary>
public sealed record RunSummary(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("overall")] string Overall,
    [property: JsonPropertyName("totals")] RunSummaryTotals Totals,
    [property: JsonPropertyName("flagProof")] RunSummaryFlagProof FlagProof,
    [property: JsonPropertyName("cases")] IReadOnlyList<RunSummaryCase> Cases,
    // pr-annotation-evidence-link: the run's project-dashboard URL, populated only when a report
    // upload succeeded. Omitted (not null) when there was no upload — a no-upload summary differs
    // from schema v1 only by the version integer.
    [property: JsonPropertyName("runUrl")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RunUrl = null,
    // github-oidc-upload: how the run authenticated its hosted upload ("token", "oidc", "none") or
    // why it could not ("oidc-exchange-failed" + reason), so a CI integration can show a
    // misconfigured permission on the PR instead of only in the job log. Omitted when null (v3 shape).
    [property: JsonPropertyName("upload")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RunSummaryUpload? Upload = null)
{
    public const int CurrentSchemaVersion = 4;
}

/// <summary>github-oidc-upload: see <see cref="RunSummary.Upload"/>. <c>reason</c> is set only for <c>oidc-exchange-failed</c>.</summary>
public sealed record RunSummaryUpload(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("reason")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason);

public sealed record RunSummaryTotals(
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("cases")] int Cases);

public sealed record RunSummaryFlagProof(
    [property: JsonPropertyName("proven")] int Proven,
    [property: JsonPropertyName("ineligible")] int Ineligible,
    [property: JsonPropertyName("regressed")] int Regressed);

public sealed record RunSummaryCase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("classification")] string? Classification,
    [property: JsonPropertyName("flagProof")] string? FlagProof,
    [property: JsonPropertyName("release")] string? Release,
    // pr-annotation-evidence-link: the dashboard evidence-view URL for this case's uploaded report,
    // set only when evidence was uploaded and accepted. Omitted (not null) otherwise.
    [property: JsonPropertyName("evidenceUrl")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EvidenceUrl = null,
    // ticket-evidence-write-back: the case's oracle.locator, carried through so a CI integration can
    // resolve a ticket to post evidence to without re-parsing case files. Omitted (not null) when the
    // case declared no locator.
    [property: JsonPropertyName("oracleLocator")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OracleLocator = null);

/// <summary>Accumulates per-case rows during the run, then produces the versioned <see cref="RunSummary"/>.</summary>
public sealed class RunSummaryBuilder
{
    private readonly List<RunSummaryCase> _cases = new();

    public void AddCase(string id, bool passed, string? classification, string? flagProofOutcome, string? release, string? evidenceUrl = null, string? oracleLocator = null)
    {
        _cases.Add(new RunSummaryCase(
            id,
            passed ? "passed" : "failed",
            classification?.ToLowerInvariant(),
            flagProofOutcome,
            string.IsNullOrWhiteSpace(release) ? null : release,
            string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl,
            string.IsNullOrWhiteSpace(oracleLocator) ? null : oracleLocator));
    }

    /// <param name="runUrl">
    /// pr-annotation-evidence-link: the project-dashboard URL returned by a successful report upload,
    /// or null when no upload happened.
    /// </param>
    public RunSummary Build(string? runUrl = null, RunSummaryUpload? upload = null)
    {
        var passed = _cases.Count(c => c.Outcome == "passed");
        var failed = _cases.Count - passed;

        var proven = _cases.Count(c => c.FlagProof == "Passed");
        var ineligible = _cases.Count(c => c.FlagProof == "Ineligible");
        var regressed = _cases.Count(c => c.FlagProof is not null && c.FlagProof != "Passed" && c.FlagProof != "Ineligible");

        return new RunSummary(
            RunSummary.CurrentSchemaVersion,
            failed == 0 ? "passed" : "failed",
            new RunSummaryTotals(passed, failed, _cases.Count),
            new RunSummaryFlagProof(proven, ineligible, regressed),
            _cases,
            string.IsNullOrWhiteSpace(runUrl) ? null : runUrl,
            upload);
    }
}

public static class RunSummaryWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// ci-pr-integration design.md D-B: the destination's parent directory must already exist.
    /// Returns a one-line error message for the caller to print, or null when the path is usable.
    /// Shares <see cref="ReportDestination"/> with the JUnit report writer (ci-report-formats).
    /// </summary>
    public static string? ValidateDestination(string path) => ReportDestination.Validate(path, "--summary-json");

    public static void Write(string path, RunSummary summary) =>
        File.WriteAllText(path, JsonSerializer.Serialize(summary, Options) + Environment.NewLine);
}
