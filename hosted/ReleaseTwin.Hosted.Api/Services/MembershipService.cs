using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// org-membership: the read side of membership, including the lazy read-repair for users provisioned
/// under the pre-membership 1:1 model (design D3). Mirrors the read-repair discipline
/// <see cref="Data.Repositories.OrganizationRepository.ParsePlanTier"/> uses for the legacy
/// <c>"Paid"</c> tier string — no backfill job, fixed on first load.
/// </summary>
public sealed class MembershipService
{
    private readonly IMembershipRepository _memberships;

    public MembershipService(IMembershipRepository memberships) => _memberships = memberships;

    /// <summary>
    /// The organizations this user belongs to. A user with no membership items but a legacy
    /// <see cref="AppUser.OrganizationId"/> is read-repaired to a single founding <c>Admin</c>
    /// membership, which is persisted before returning.
    /// </summary>
    public async Task<IReadOnlyList<Membership>> GetMembershipsAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var memberships = await _memberships.ListOrgsByUserAsync(user.Id, cancellationToken);
        if (memberships.Count > 0)
        {
            return memberships;
        }

        if (user.OrganizationId == Guid.Empty)
        {
            return memberships;
        }

        var repaired = new Membership
        {
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            Role = MembershipRole.Admin,
            CreatedAt = user.CreatedAt == default ? DateTimeOffset.UtcNow : user.CreatedAt,
        };
        await _memberships.PutAsync(repaired, cancellationToken);
        return [repaired];
    }

    /// <summary>The user's membership in one organization, applying the same read-repair, or null if they are not a member.</summary>
    public async Task<Membership?> GetMembershipAsync(AppUser user, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var direct = await _memberships.GetAsync(organizationId, user.Id, cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        if (user.OrganizationId != organizationId || organizationId == Guid.Empty)
        {
            return null;
        }

        // Legacy user, no membership item yet: repair only for their own legacy org.
        var all = await GetMembershipsAsync(user, cancellationToken);
        return all.FirstOrDefault(m => m.OrganizationId == organizationId);
    }

    /// <summary>
    /// org-membership: the last remaining admin of an organization SHALL NOT be removable or
    /// demotable. Call before a member-remove or a role change that would drop an admin. A no-op when
    /// the target is not currently an admin.
    /// </summary>
    public async Task EnsureNotLastAdminAsync(Guid organizationId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var members = await _memberships.ListMembersByOrgAsync(organizationId, cancellationToken);
        var target = members.FirstOrDefault(m => m.UserId == targetUserId);
        if (target is null || target.Role != MembershipRole.Admin)
        {
            return;
        }

        if (members.Count(m => m.Role == MembershipRole.Admin) <= 1)
        {
            throw new ForbiddenException("The organization must keep at least one admin.");
        }
    }
}
