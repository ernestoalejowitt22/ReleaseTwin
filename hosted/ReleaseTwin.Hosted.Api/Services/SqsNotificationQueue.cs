using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>run-notifications: enqueues a <see cref="RunNotification"/> onto the SQS queue the
/// dispatcher Lambda drains. Bound only when <c>Notifications:QueueUrl</c> is configured.</summary>
public sealed class SqsNotificationQueue : INotificationQueue
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;
    private readonly ILogger<SqsNotificationQueue> _logger;

    public SqsNotificationQueue(IAmazonSQS sqs, string queueUrl, ILogger<SqsNotificationQueue> logger)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
        _logger = logger;
    }

    public async Task EnqueueAsync(RunNotification notification, CancellationToken cancellationToken = default)
    {
        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(notification, Json),
        }, cancellationToken);
        _logger.LogInformation("run_notification_enqueued project={ProjectId} report={ReportId} result={Result}",
            notification.ProjectId, notification.ReportId, notification.Result);
    }
}
