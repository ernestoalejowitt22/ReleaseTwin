using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing (design.md D3): the webhook idempotency store. One overloaded single-table item per
/// Merchant-of-Record event id, with an epoch-seconds <c>ExpiresAt</c> so DynamoDB TTL reaps it ~30d
/// after processing — well past Polar's retry window. "Check before process, record after process":
/// a duplicate delivery finds the item and is a 200 no-op; a delivery whose processing fails never
/// reaches the record step and is safely redelivered.
/// </summary>
public sealed class ProcessedBillingEventRepository
{
    /// <summary>design.md D3: 30d comfortably exceeds Polar's ~3-day retry window.</summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    private readonly IHostedTable _table;

    public ProcessedBillingEventRepository(IHostedTable table) => _table = table;

    public async Task<bool> HasProcessedAsync(string providerEventId, CancellationToken cancellationToken = default)
    {
        var key = Keys.BillingEvent(providerEventId);
        return await _table.GetItemAsync(key, key, cancellationToken) is not null;
    }

    /// <summary>
    /// Records the event as processed. A concurrent recorder that got there first
    /// (<see cref="ConditionalCheckFailedException"/>) is fine — the state writes that preceded this
    /// are all idempotent "set state X", so the duplicate changed nothing.
    /// </summary>
    public async Task MarkProcessedAsync(string providerEventId, string eventType, CancellationToken cancellationToken = default)
    {
        var key = Keys.BillingEvent(providerEventId);
        var now = DateTimeOffset.UtcNow;
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = Attrs.S(key),
            ["SK"] = Attrs.S(key),
            ["EntityType"] = Attrs.S("ProcessedBillingEvent"),
            ["ProviderEventId"] = Attrs.S(providerEventId),
            ["EventType"] = Attrs.S(eventType),
            ["ProcessedAt"] = Attrs.S(now.ToString("O")),
            ["ExpiresAt"] = Attrs.N((now + RetentionWindow).ToUnixTimeSeconds()),
        };

        try
        {
            await _table.PutItemAsync(item, conditionExpression: "attribute_not_exists(PK)", cancellationToken: cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            // Already recorded by a concurrent delivery — nothing to do.
        }
    }
}
