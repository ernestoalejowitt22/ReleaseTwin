namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>usage-metering: one low-volume item per organization per calendar month, atomically incremented at ingest time.</summary>
public sealed class UsageCounter
{
    public Guid OrganizationId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public long CaseReportCount { get; set; }
    public long FlagProofReportCount { get; set; }
}
