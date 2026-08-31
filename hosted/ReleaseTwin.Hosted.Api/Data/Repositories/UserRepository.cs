using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IHostedTable _table;

    public UserRepository(IHostedTable table) => _table = table;

    public async Task<AppUser?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.User(clerkUserId), Keys.User(clerkUserId), cancellationToken);
        return item is null ? null : ToUser(item);
    }

    public async Task CreateWithOrganizationAsync(Organization organization, AppUser user, CancellationToken cancellationToken = default)
    {
        await _table.TransactWritePutAsync(
        [
            (OrganizationRepository.ToItem(organization), null),
            (ToItem(user), "attribute_not_exists(PK)"),
        ], cancellationToken);
    }

    public async Task CreateWithOrganizationAsync(Organization organization, AppUser user, Membership foundingMembership, CancellationToken cancellationToken = default)
    {
        await _table.TransactWritePutAsync(
        [
            (OrganizationRepository.ToItem(organization), null),
            (ToItem(user), "attribute_not_exists(PK)"),
            (MembershipRepository.ToItem(foundingMembership), null),
        ], cancellationToken);
    }

    public async Task CreateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _table.PutItemAsync(ToItem(user), "attribute_not_exists(PK)", cancellationToken);
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(AppUser user) => new()
    {
        ["PK"] = Attrs.S(Keys.User(user.ClerkUserId)),
        ["SK"] = Attrs.S(Keys.User(user.ClerkUserId)),
        ["EntityType"] = Attrs.S("User"),
        ["Id"] = Attrs.S(user.Id.ToString()),
        ["ClerkUserId"] = Attrs.S(user.ClerkUserId),
        ["DisplayName"] = Attrs.S(user.DisplayName),
        ["Email"] = user.Email is null ? new() { NULL = true } : Attrs.S(user.Email),
        ["CreatedAt"] = Attrs.S(user.CreatedAt.ToString("O")),
        ["OrganizationId"] = Attrs.S(user.OrganizationId.ToString()),
    };

    private static AppUser ToUser(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ClerkUserId = item.GetS("ClerkUserId"),
        DisplayName = item.GetS("DisplayName"),
        Email = item.GetSOrNull("Email"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        OrganizationId = item.GetGuid("OrganizationId"),
    };
}
