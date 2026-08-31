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
    [property: JsonPropertyName("cases")] IReadOnlyList<RunSummaryCase> Cases)
{
    public const int CurrentSchemaVersion = 1;
}

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
    [property: JsonPropertyName("release")] string? Release);

/// <summary>Accumulates per-case rows during the run, then produces the versioned <see cref="RunSummary"/>.</summary>
public sealed class RunSummaryBuilder
{
    private readonly List<RunSummaryCase> _cases = new();

    public void AddCase(string id, bool passed, string? classification, string? flagProofOutcome, string? release)
    {
        _cases.Add(new RunSummaryCase(
            id,
            passed ? "passed" : "failed",
            classification?.ToLowerInvariant(),
            flagProofOutcome,
            string.IsNullOrWhiteSpace(release) ? null : release));
    }

    public RunSummary Build()
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
            _cases);
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
    /// </summary>
    public static string? ValidateDestination(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return !string.IsNullOrEmpty(directory) && !Directory.Exists(directory)
            ? $"--summary-json: directory does not exist: {directory}"
            : null;
    }

    public static void Write(string path, RunSummary summary) =>
        File.WriteAllText(path, JsonSerializer.Serialize(summary, Options) + Environment.NewLine);
}
