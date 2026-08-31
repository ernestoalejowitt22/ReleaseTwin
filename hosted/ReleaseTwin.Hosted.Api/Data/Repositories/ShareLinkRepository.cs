using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class ShareLinkRepository : IShareLinkRepository
{
    private readonly IHostedTable _table;

    public ShareLinkRepository(IHostedTable table) => _table = table;

    /// <summary>evidence-sharing (design D7): the token is <c>&lt;reportId&gt;.&lt;random&gt;</c> — 32 random
    /// bytes, base64url. Only its hash is stored.</summary>
    public static string NewToken(Guid reportId)
    {
        Span<byte> bytes = stackalloc byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var random = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"{reportId}.{random}";
    }

    public static bool TryParseReportId(string token, out Guid reportId)
    {
        reportId = Guid.Empty;
        var dot = token.IndexOf('.');
        return dot > 0 && Guid.TryParse(token.AsSpan(0, dot), out reportId);
    }

    public Task PutAsync(ShareLink link, CancellationToken cancellationToken = default) =>
        _table.PutItemAsync(ToItem(link), cancellationToken: cancellationToken);

    public async Task<ShareLink?> GetByTokenHashAsync(Guid reportId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Run(reportId), Keys.ShareLink(tokenHash), cancellationToken);
        return item is null ? null : ToLink(item);
    }

    public async Task<IReadOnlyList<ShareLink>> ListByReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Run(reportId), "SHARE#", cancellationToken: cancellationToken);
        return items.Select(ToLink).ToList();
    }

    public async Task RevokeAsync(Guid reportId, Guid linkId, CancellationToken cancellationToken = default)
    {
        var links = await ListByReportAsync(reportId, cancellationToken);
        var target = links.FirstOrDefault(l => l.Id == linkId);
        if (target is null || target.State == ShareLinkState.Revoked)
        {
            return;
        }

        target.State = ShareLinkState.Revoked;
        await PutAsync(target, cancellationToken);
    }

    public async Task DeleteAllForReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        foreach (var link in await ListByReportAsync(reportId, cancellationToken))
        {
            await _table.DeleteItemAsync(Keys.Run(reportId), Keys.ShareLink(link.TokenHash), cancellationToken);
        }
    }

    private static Dictionary<string, AttributeValue> ToItem(ShareLink l)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Run(l.ReportId)),
            ["SK"] = Attrs.S(Keys.ShareLink(l.TokenHash)),
            ["EntityType"] = Attrs.S("ShareLink"),
            ["Id"] = Attrs.S(l.Id.ToString()),
            ["ReportId"] = Attrs.S(l.ReportId.ToString()),
            ["ProjectId"] = Attrs.S(l.ProjectId.ToString()),
            ["OrganizationId"] = Attrs.S(l.OrganizationId.ToString()),
            ["ReportKind"] = Attrs.S(l.ReportKind),
            ["TokenHash"] = Attrs.S(l.TokenHash),
            ["State"] = Attrs.S(l.State.ToString()),
            ["ExpiresAt"] = Attrs.S(l.ExpiresAt.ToString("O")),
            ["CreatedAt"] = Attrs.S((l.CreatedAt == default ? DateTimeOffset.UtcNow : l.CreatedAt).ToString("O")),
            ["CreatedByUserId"] = Attrs.S(l.CreatedByUserId.ToString()),
            ["CaseId"] = Attrs.S(l.CaseId),
            ["Result"] = Attrs.S(l.Result),
            ["FixtureSha256"] = Attrs.S(l.FixtureSha256),
        };
        item.SetIfNotNull("Classification", Attrs.SOrNull(l.Classification));
        return item;
    }

    private static ShareLink ToLink(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ReportId = item.GetGuid("ReportId"),
        ProjectId = item.GetGuid("ProjectId"),
        OrganizationId = item.GetGuid("OrganizationId"),
        ReportKind = item.GetS("ReportKind"),
        TokenHash = item.GetS("TokenHash"),
        State = Enum.TryParse<ShareLinkState>(item.GetSOrNull("State"), ignoreCase: true, out var state) ? state : ShareLinkState.Active,
        ExpiresAt = item.GetDateTimeOffset("ExpiresAt"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        CreatedByUserId = item.GetGuid("CreatedByUserId"),
        CaseId = item.GetS("CaseId"),
        Result = item.GetS("Result"),
        Classification = item.GetSOrNull("Classification"),
        FixtureSha256 = item.GetS("FixtureSha256"),
    };
}
