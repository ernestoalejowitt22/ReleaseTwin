namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>run-notifications: the minimal slice of an AWS SQS Lambda event the dispatcher needs —
/// message body + id — parsed directly rather than pulling in <c>Amazon.Lambda.SQSEvents</c>.</summary>
public sealed class SqsBatch
{
    public List<SqsRecord>? Records { get; set; }
}

public sealed class SqsRecord
{
    public string? MessageId { get; set; }
    public string? Body { get; set; }
}
