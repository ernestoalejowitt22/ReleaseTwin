namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// evidence-store: one already-redacted run evidence document, uploaded alongside a case or
/// flag-proof report. The server never inspects <see cref="DocumentJson"/> (ingest-api spec: stored
/// as received) — it is opaque, redacted-by-the-CLI JSON. Screenshot blobs live in the blob store,
/// referenced by the ids in <see cref="ScreenshotIds"/>.
/// </summary>
public sealed class UploadedRunEvidence
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>The report this evidence belongs to (a <see cref="UploadedCaseReport"/> or <see cref="UploadedFlagProofReport"/> id).</summary>
    public Guid ReportId { get; set; }

    /// <summary>"case" or "flag-proof".</summary>
    public required string ReportKind { get; set; }

    public required string DocumentJson { get; set; }

    public IReadOnlyList<string> ScreenshotIds { get; set; } = Array.Empty<string>();

    public DateTimeOffset UploadedAt { get; set; }
}
