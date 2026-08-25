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

    internal static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(Organization org) => new()
    {
        ["PK"] = Attrs.S(Keys.Org(org.Id)),
        ["SK"] = Attrs.S(Keys.Org(org.Id)),
        ["EntityType"] = Attrs.S("Organization"),
        ["Id"] = Attrs.S(org.Id.ToString()),
        ["Name"] = Attrs.S(org.Name),
        ["CreatedAt"] = Attrs.S(org.CreatedAt.ToString("O")),
        ["PlanTier"] = Attrs.S(org.PlanTier.ToString()),
    };

    internal static Organization ToOrganization(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        Name = item.GetS("Name"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        PlanTier = item.TryGetValue("PlanTier", out var v) && v.S is not null ? Enum.Parse<PlanTier>(v.S) : PlanTier.Free,
    };
}
