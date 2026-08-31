using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class InvitationRepository : IInvitationRepository
{
    private readonly IHostedTable _table;

    public InvitationRepository(IHostedTable table) => _table = table;

    /// <summary>org-membership design D2: the invite token is <c>&lt;orgId&gt;.&lt;random&gt;</c> so the
    /// accept flow can find the item from the token alone, with no secondary index.</summary>
    public static string NewToken(Guid organizationId)
    {
        Span<byte> bytes = stackalloc byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var random = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"{organizationId}.{random}";
    }

    public static bool TryParseOrganizationId(string token, out Guid organizationId)
    {
        organizationId = Guid.Empty;
        var dot = token.IndexOf('.');
        return dot > 0 && Guid.TryParse(token.AsSpan(0, dot), out organizationId);
    }

    public Task PutAsync(Invitation invitation, CancellationToken cancellationToken = default) =>
        _table.PutItemAsync(ToItem(invitation), cancellationToken: cancellationToken);

    public async Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryParseOrganizationId(token, out var organizationId))
        {
            return null;
        }

        var item = await _table.GetItemAsync(Keys.Org(organizationId), Keys.Invite(token), cancellationToken);
        return item is null ? null : ToInvitation(item);
    }

    public async Task<IReadOnlyList<Invitation>> ListByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Org(organizationId), "INVITE#", cancellationToken: cancellationToken);
        return items.Select(ToInvitation).ToList();
    }

    public Task DeleteAsync(Guid organizationId, string token, CancellationToken cancellationToken = default) =>
        _table.DeleteItemAsync(Keys.Org(organizationId), Keys.Invite(token), cancellationToken);

    public Task ClaimAsync(Invitation invitation, Membership membership, CancellationToken cancellationToken = default)
    {
        var claimItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Org(invitation.OrganizationId)),
            ["SK"] = Attrs.S(Keys.InviteClaim(invitation.Token)),
            ["EntityType"] = Attrs.S("InvitationClaim"),
            ["ClaimedByUserId"] = Attrs.S(membership.UserId.ToString()),
            ["ClaimedAt"] = Attrs.S(DateTimeOffset.UtcNow.ToString("O")),
        };

        var accepted = ToItem(invitation);
        accepted["State"] = Attrs.S(InvitationState.Accepted.ToString());

        // All-or-nothing: the claim marker enforces single-use, the membership condition guards a
        // duplicate join, and the invitation flip is bookkeeping for listings.
        return _table.TransactWritePutAsync(
        [
            (claimItem, "attribute_not_exists(PK)"),
            (MembershipRepository.ToItem(membership), "attribute_not_exists(PK)"),
            (accepted, null),
        ], cancellationToken);
    }

    private static Dictionary<string, AttributeValue> ToItem(Invitation i) => new()
    {
        ["PK"] = Attrs.S(Keys.Org(i.OrganizationId)),
        ["SK"] = Attrs.S(Keys.Invite(i.Token)),
        ["EntityType"] = Attrs.S("Invitation"),
        ["OrganizationId"] = Attrs.S(i.OrganizationId.ToString()),
        ["Token"] = Attrs.S(i.Token),
        ["Email"] = Attrs.S(i.Email),
        ["Role"] = Attrs.S(i.Role.ToString()),
        ["State"] = Attrs.S(i.State.ToString()),
        ["ExpiresAt"] = Attrs.S(i.ExpiresAt.ToString("O")),
        ["CreatedAt"] = Attrs.S((i.CreatedAt == default ? DateTimeOffset.UtcNow : i.CreatedAt).ToString("O")),
        ["CreatedByUserId"] = Attrs.S(i.CreatedByUserId.ToString()),
    };

    private static Invitation ToInvitation(Dictionary<string, AttributeValue> item) => new()
    {
        OrganizationId = item.GetGuid("OrganizationId"),
        Token = item.GetS("Token"),
        Email = item.GetS("Email"),
        Role = Enum.TryParse<MembershipRole>(item.GetSOrNull("Role"), ignoreCase: true, out var role) ? role : MembershipRole.Member,
        State = Enum.TryParse<InvitationState>(item.GetSOrNull("State"), ignoreCase: true, out var state) ? state : InvitationState.Pending,
        ExpiresAt = item.GetDateTimeOffset("ExpiresAt"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        CreatedByUserId = item.GetGuid("CreatedByUserId"),
    };
}
