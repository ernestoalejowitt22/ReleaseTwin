namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>evidence-sharing: lifecycle state of a <see cref="ShareLink"/>.</summary>
public enum ShareLinkState
{
    Active,
    Revoked,
}

/// <summary>
/// evidence-sharing (design D7): a per-run, revocable, read-only link that renders one run's already
/// redacted evidence to an unauthenticated viewer. Only the SHA-256 hash of the token is stored. The
/// report metadata a shared viewer is allowed to see (<see cref="CaseId"/>, <see cref="Result"/>,
/// <see cref="Classification"/>, <see cref="FixtureSha256"/>) is denormalised here at creation time,
/// so resolving a link never has to reach into project- or org-scoped data.
///
/// Item shape: <c>PK=RUN#&lt;reportId&gt;</c>, <c>SK=SHARE#&lt;tokenHash&gt;</c>.
/// </summary>
public sealed class ShareLink
{
    public required Guid Id { get; set; }
    public required Guid ReportId { get; set; }
    public required Guid ProjectId { get; set; }
    public required Guid OrganizationId { get; set; }
    public required string ReportKind { get; set; }

    public required string TokenHash { get; set; }

    public ShareLinkState State { get; set; } = ShareLinkState.Active;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required Guid CreatedByUserId { get; set; }

    // Denormalised report metadata — the whole of what a shared viewer may see about the run itself.
    public required string CaseId { get; set; }
    public required string Result { get; set; }
    public string? Classification { get; set; }
    public required string FixtureSha256 { get; set; }

    public bool IsResolvable(DateTimeOffset now) => State == ShareLinkState.Active && now < ExpiresAt;
}
