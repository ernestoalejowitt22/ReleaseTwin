using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class NotificationTargetRepository : INotificationTargetRepository
{
    private readonly IHostedTable _table;

    public NotificationTargetRepository(IHostedTable table) => _table = table;

    public async Task<IReadOnlyList<NotificationTarget>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "NOTIFYTARGET#", cancellationToken: cancellationToken);
        return items.Select(ToTarget).ToList();
    }

    public async Task<NotificationTarget?> GetAsync(Guid projectId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Project(projectId), Keys.NotificationTarget(targetId), cancellationToken);
        return item is null ? null : ToTarget(item);
    }

    public Task PutAsync(NotificationTarget target, CancellationToken cancellationToken = default) =>
        _table.PutItemAsync(ToItem(target), cancellationToken: cancellationToken);

    public Task DeleteAsync(Guid projectId, Guid targetId, CancellationToken cancellationToken = default) =>
        _table.DeleteItemAsync(Keys.Project(projectId), Keys.NotificationTarget(targetId), cancellationToken);

    public async Task RecordOutcomeAsync(Guid projectId, Guid targetId, string outcome, DateTimeOffset attemptedAt, CancellationToken cancellationToken = default)
    {
        var target = await GetAsync(projectId, targetId, cancellationToken);
        if (target is null)
        {
            return;
        }

        target.LastOutcome = outcome;
        target.LastAttemptAt = attemptedAt;
        await PutAsync(target, cancellationToken);
    }

    private static Dictionary<string, AttributeValue> ToItem(NotificationTarget t)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Project(t.ProjectId)),
            ["SK"] = Attrs.S(Keys.NotificationTarget(t.Id)),
            ["EntityType"] = Attrs.S("NotificationTarget"),
            ["Id"] = Attrs.S(t.Id.ToString()),
            ["ProjectId"] = Attrs.S(t.ProjectId.ToString()),
            ["Kind"] = Attrs.S(t.Kind.ToString()),
            ["Url"] = Attrs.S(t.Url),
            ["Enabled"] = Attrs.Bool(t.Enabled),
            ["CreatedAt"] = Attrs.S((t.CreatedAt == default ? DateTimeOffset.UtcNow : t.CreatedAt).ToString("O")),
        };
        item.SetIfNotNull("LastOutcome", Attrs.SOrNull(t.LastOutcome));
        item.SetIfNotNull("LastAttemptAt", Attrs.SOrNull(t.LastAttemptAt?.ToString("O")));
        return item;
    }

    private static NotificationTarget ToTarget(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ProjectId = item.GetGuid("ProjectId"),
        Kind = Enum.TryParse<NotificationTargetKind>(item.GetSOrNull("Kind"), ignoreCase: true, out var kind) ? kind : NotificationTargetKind.Webhook,
        Url = item.GetS("Url"),
        Enabled = item.GetBool("Enabled"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        LastOutcome = item.GetSOrNull("LastOutcome"),
        LastAttemptAt = item.GetSOrNull("LastAttemptAt") is { } s ? DateTimeOffset.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
    };
}
