namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// run-notifications: the message the ingest path enqueues when a run fails, and the notification
/// dispatcher Lambda consumes. Deliberately carries no fixture content, response bodies, or
/// credentials — only the metadata already on the uploaded report.
/// </summary>
public sealed record RunNotification(
    Guid OrganizationId,
    Guid ProjectId,
    Guid ReportId,
    string ReportKind,
    string CaseId,
    string Result,
    string? Classification);

/// <summary>
/// run-notifications: the seam between ingest (producer) and the dispatcher Lambda (consumer). The
/// real implementation is <see cref="SqsNotificationQueue"/>; when no queue is configured the
/// no-op <see cref="NullNotificationQueue"/> is bound so ingest is unaffected.
/// </summary>
public interface INotificationQueue
{
    Task EnqueueAsync(RunNotification notification, CancellationToken cancellationToken = default);
}

public sealed class NullNotificationQueue : INotificationQueue
{
    public Task EnqueueAsync(RunNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
