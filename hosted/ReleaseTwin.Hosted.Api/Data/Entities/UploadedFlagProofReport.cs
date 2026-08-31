namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// Metadata-only mirror of ReleaseTwin.Core.FlagProofResult. Shown distinctly from ordinary case
/// results on the dashboard (dashboard spec: "Flag-proof outcomes are shown distinctly").
/// </summary>
public sealed class UploadedFlagProofReport
{
    public Guid Id { get; set; }
    public required string CaseId { get; set; }
    public required string OracleLocator { get; set; }
    public required string BuildIdentity { get; set; }
    public required string Outcome { get; set; }
    public bool? KnownBadLegPassed { get; set; }
    public bool? KnownGoodLegPassed { get; set; }

    /// <summary>release-readiness-rollup: the uploaded case's optional free-form <c>release</c> label, stored verbatim.</summary>
    public string? Release { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public Guid ProjectId { get; set; }
}
