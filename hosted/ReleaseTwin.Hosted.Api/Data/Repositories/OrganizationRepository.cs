using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly IHostedTable _table;

    public OrganizationRepository(IHostedTable table) => _table = table;

    public async Task<Organization?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Org(organizationId), Keys.Org(organizationId), cancellationToken);
        return item is null ? null : ToOrganization(item);
    }

    public async Task SetPlanTierAsync(Guid organizationId, PlanTier tier, CancellationToken cancellationToken = default)
    {
        var org = await GetAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot set plan tier: organization {organizationId} not found.");
        org.PlanTier = tier;
        await _table.PutItemAsync(ToItem(org), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// billing: the single billing-linkage mutation point (webhook + reconciliation). One read-mutate-put,
    /// same shape as <see cref="SetPlanTierAsync"/>. All writes are "set to state X" so a redelivered
    /// event replays safely.
    /// </summary>
    public async Task SetBillingAsync(Guid organizationId, BillingStatus status, DateTimeOffset since, BillingCadence? cadence, string? customerId, string? subscriptionId, CancellationToken cancellationToken = default)
    {
        var org = await GetAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot set billing: organization {organizationId} not found.");
        org.BillingStatus = status;
        org.BillingStatusSince = since;
        org.BillingCadence = cadence;
        org.PolarCustomerId = customerId;
        org.PolarSubscriptionId = subscriptionId;
        await _table.PutItemAsync(ToItem(org), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _table.ScanByEntityTypeAsync("Organization", cancellationToken);
        return items.Select(ToOrganization).ToList();
    }

    public async Task MarkIngestedRealRunAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await GetAsync(organizationId, cancellationToken);
        if (org is null || org.HasIngestedRealRun)
        {
            return;
        }

        org.HasIngestedRealRun = true;
        await _table.PutItemAsync(ToItem(org), cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _table.DeleteItemAsync(Keys.Org(organizationId), Keys.Org(organizationId), cancellationToken);

    public Task CreateWithFounderAsync(Organization organization, Membership founder, CancellationToken cancellationToken = default) =>
        _table.TransactWritePutAsync(
        [
            (ToItem(organization), "attribute_not_exists(PK)"),
            (MembershipRepository.ToItem(founder), null),
        ], cancellationToken);

    internal static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(Organization org)
    {
        var item = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Org(org.Id)),
            ["SK"] = Attrs.S(Keys.Org(org.Id)),
            ["EntityType"] = Attrs.S("Organization"),
            ["Id"] = Attrs.S(org.Id.ToString()),
            ["Name"] = Attrs.S(org.Name),
            ["CreatedAt"] = Attrs.S(org.CreatedAt.ToString("O")),
            ["PlanTier"] = Attrs.S(org.PlanTier.ToString()),
            ["BillingStatus"] = Attrs.S(org.BillingStatus.ToString()),
            ["BillingStatusSince"] = Attrs.S((org.BillingStatusSince == default ? org.CreatedAt : org.BillingStatusSince).ToString("O")),
        };
        item.SetIfNotNull("BillingCadence", Attrs.SOrNull(org.BillingCadence?.ToString()));
        item.SetIfNotNull("PolarCustomerId", Attrs.SOrNull(org.PolarCustomerId));
        item.SetIfNotNull("PolarSubscriptionId", Attrs.SOrNull(org.PolarSubscriptionId));
        if (org.HasIngestedRealRun)
        {
            item["HasIngestedRealRun"] = Attrs.Bool(true);
        }
        return item;
    }

    internal static Organization ToOrganization(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item)
    {
        var createdAt = item.GetDateTimeOffset("CreatedAt");
        return new()
        {
            Id = item.GetGuid("Id"),
            Name = item.GetS("Name"),
            CreatedAt = createdAt,
            PlanTier = ParsePlanTier(item.TryGetValue("PlanTier", out var v) ? v.S : null),
            // billing: legacy rows predate these attributes — default to the unlinked, active state
            // (same read-repair discipline as ParsePlanTier; rewritten on the next put). No backfill.
            BillingStatus = Enum.TryParse<BillingStatus>(item.GetSOrNull("BillingStatus"), ignoreCase: true, out var status)
                ? status
                : BillingStatus.Active,
            BillingStatusSince = item.TryGetValue("BillingStatusSince", out var since) && since.S is not null
                ? DateTimeOffset.Parse(since.S, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : createdAt,
            BillingCadence = Enum.TryParse<BillingCadence>(item.GetSOrNull("BillingCadence"), ignoreCase: true, out var cadence)
                ? cadence
                : null,
            PolarCustomerId = item.GetSOrNull("PolarCustomerId"),
            PolarSubscriptionId = item.GetSOrNull("PolarSubscriptionId"),
            HasIngestedRealRun = item.TryGetValue("HasIngestedRealRun", out var ingested) && ingested.BOOL == true,
        };
    }

    /// <summary>
    /// plan-catalog-and-entitlements: the enum went Free/Paid → Free/Team/Enterprise. Any row still
    /// storing the old "Paid" string is read-repaired to <see cref="PlanTier.Team"/> (and rewritten
    /// to "Team" on its next <see cref="SetPlanTierAsync"/> / put), so a missed backfill row degrades
    /// gracefully instead of throwing on <see cref="Enum.Parse{TEnum}(string)"/>.
    /// </summary>
    internal static PlanTier ParsePlanTier(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return PlanTier.Free;
        }

        if (string.Equals(stored, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return PlanTier.Team;
        }

        return Enum.TryParse<PlanTier>(stored, ignoreCase: true, out var tier) ? tier : PlanTier.Free;
    }
}
