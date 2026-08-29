using System.Text.Json;

namespace ReleaseTwin.Hosted.Api.Contracts;

/// <summary>
/// design.md D1: the ingest API's own stable wire contract, deliberately decoupled from
/// ReleaseTwin.Core's CaseReport/FlagProofResult types so the hosted API doesn't break every time
/// those internal types evolve (they already have, twice, in this project's history).
///
/// ingest-api spec, "The ingest contract has no field for sensitive content": every field here is
/// metadata — no fixture content, no response bodies, no credentials.
/// </summary>
public sealed class IngestCaseReportRequest
{
    public required string CaseId { get; init; }
    public required string OracleLocator { get; init; }
    public required string FixtureSha256 { get; init; }
    public required bool Passed { get; init; }
    public string? Classification { get; init; }
    public string? FailureDetail { get; init; }
    public required string CleanupStatus { get; init; }
    public required long DurationMs { get; init; }

    /// <summary>
    /// evidence-capture: an optional, already-redacted evidence document. Opaque here — the ingest
    /// API never inspects it for sensitive content (redaction is the caller's completed
    /// responsibility, done in their CLI). Absent ⇒ the request is exactly the pre-evidence shape.
    /// This carries no field for a credential or token value.
    /// </summary>
    public JsonElement? Evidence { get; init; }
}

public sealed class IngestFlagProofReportRequest
{
    public required string CaseId { get; init; }
    public required string OracleLocator { get; init; }
    public required string BuildIdentity { get; init; }
    public required string Outcome { get; init; }
    public bool? KnownBadLegPassed { get; init; }
    public bool? KnownGoodLegPassed { get; init; }

    /// <summary>evidence-capture: see <see cref="IngestCaseReportRequest.Evidence"/>.</summary>
    public JsonElement? Evidence { get; init; }
}
