namespace ReleaseTwin.Hosted.Api.Data.Entities;

public sealed class Project
{
    /// <summary>evidence-store: system default retention window when a project has never set its own.</summary>
    public const int DefaultEvidenceRetentionDays = 30;

    /// <summary>evidence-store: the longest window a customer is allowed to set.</summary>
    public const int MaxEvidenceRetentionDays = 365;

    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>evidence-capture: the hosted per-project default the CLI reads when RELEASETWIN_EVIDENCE is unset.</summary>
    public bool EvidenceCaptureDefault { get; set; }

    /// <summary>evidence-store: this project's evidence retention window in days. Defaults to <see cref="DefaultEvidenceRetentionDays"/>.</summary>
    public int EvidenceRetentionDays { get; set; } = DefaultEvidenceRetentionDays;
}
