using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class MembershipRepository : IMembershipRepository
{
    private readonly IHostedTable _table;

    public MembershipRepository(IHostedTable table) => _table = table;

    public async Task<IReadOnlyList<Membership>> ListMembersByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Org(organizationId), "MEMBER#", cancellationToken: cancellationToken);
        return items.Select(ToMembership).ToList();
    }

    public async Task<IReadOnlyList<Membership>> ListOrgsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Overloaded GSI1: membership items are keyed GSI1PK=USER#<guid>, a namespace no other
        // GSI1 writer uses (ApiToken uses GSI1PK=PROJECT#<guid>). EntityType filter is belt-and-braces.
        var items = await _table.QueryGsiAsync("GSI1", Keys.UserId(userId), cancellationToken);
        return items
            .Where(i => i.GetSOrNull("EntityType") == "Membership")
            .Select(ToMembership)
            .ToList();
    }

    public async Task<Membership?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Org(organizationId), Keys.Member(userId), cancellationToken);
        return item is null ? null : ToMembership(item);
    }

    public Task PutAsync(Membership membership, CancellationToken cancellationToken = default) =>
        _table.PutItemAsync(ToItem(membership), cancellationToken: cancellationToken);

    public Task DeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        _table.DeleteItemAsync(Keys.Org(organizationId), Keys.Member(userId), cancellationToken);

    internal static Dictionary<string, AttributeValue> ToItem(Membership m)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Org(m.OrganizationId)),
            ["SK"] = Attrs.S(Keys.Member(m.UserId)),
            ["EntityType"] = Attrs.S("Membership"),
            ["OrganizationId"] = Attrs.S(m.OrganizationId.ToString()),
            ["UserId"] = Attrs.S(m.UserId.ToString()),
            ["Role"] = Attrs.S(m.Role.ToString()),
            ["CreatedAt"] = Attrs.S((m.CreatedAt == default ? DateTimeOffset.UtcNow : m.CreatedAt).ToString("O")),
            ["GSI1PK"] = Attrs.S(Keys.UserId(m.UserId)),
            ["GSI1SK"] = Attrs.S(Keys.Org(m.OrganizationId)),
        };
        item.SetIfNotNull("DisplayName", Attrs.SOrNull(m.DisplayName));
        item.SetIfNotNull("Email", Attrs.SOrNull(m.Email));
        return item;
    }

    private static Membership ToMembership(Dictionary<string, AttributeValue> item) => new()
    {
        OrganizationId = item.GetGuid("OrganizationId"),
        UserId = item.GetGuid("UserId"),
        Role = Enum.TryParse<MembershipRole>(item.GetSOrNull("Role"), ignoreCase: true, out var role) ? role : MembershipRole.Member,
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        DisplayName = item.GetSOrNull("DisplayName"),
        Email = item.GetSOrNull("Email"),
    };
}
