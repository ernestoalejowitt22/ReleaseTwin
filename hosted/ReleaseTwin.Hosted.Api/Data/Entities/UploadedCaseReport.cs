namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// Metadata-only mirror of ReleaseTwin.Core.CaseReport (design.md D1 — a stable ingest contract, not
/// the Core type itself). Never contains fixture content, response bodies, or credentials.
/// </summary>
public sealed class UploadedCaseReport
{
    public Guid Id { get; set; }
    public required string CaseId { get; set; }
    public required string OracleLocator { get; set; }
    public required string FixtureSha256 { get; set; }
    public bool Passed { get; set; }
    public string? Classification { get; set; }
    public string? FailureDetail { get; set; }

    /// <summary>release-readiness-rollup: the uploaded case's optional free-form <c>release</c> label, stored verbatim. Null for a report uploaded without one.</summary>
    public string? Release { get; set; }
    public required string CleanupStatus { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public Guid ProjectId { get; set; }
}
