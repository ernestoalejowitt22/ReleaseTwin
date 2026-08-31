namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>run-notifications: how a <see cref="NotificationTarget"/> delivers. <see cref="Slack"/> posts
/// a Slack-shaped <c>{ "text": ... }</c> body to an incoming-webhook URL; <see cref="Webhook"/> posts
/// the structured JSON payload to an arbitrary HTTPS endpoint.</summary>
public enum NotificationTargetKind
{
    Slack,
    Webhook,
}

/// <summary>
/// run-notifications: one outbound destination a project's run-failure notifications are delivered to.
/// Item shape: <c>PK=PROJECT#&lt;projectId&gt;</c>, <c>SK=NOTIFYTARGET#&lt;id&gt;</c>.
/// </summary>
public sealed class NotificationTarget
{
    public required Guid Id { get; set; }
    public required Guid ProjectId { get; set; }
    public required NotificationTargetKind Kind { get; set; }
    public required string Url { get; set; }
    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The most recent delivery result — <c>"success"</c> or <c>"failed: &lt;reason&gt;"</c> — or null if never attempted.</summary>
    public string? LastOutcome { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
