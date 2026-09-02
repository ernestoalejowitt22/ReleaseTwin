using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// org-membership: the write side of teams — creating additional organizations, issuing and accepting
/// invitations, and changing or removing members. Role/last-admin checks live in
/// <see cref="MembershipService"/>; endpoints do the capability gate before calling in here.
/// </summary>
public sealed class OrganizationMembersService
{
    /// <summary>Default invitation lifetime (design "Open Questions" — a constant, not a design decision).</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(14);

    private readonly IOrganizationRepository _organizations;
    private readonly IMembershipRepository _memberships;
    private readonly IInvitationRepository _invitations;
    private readonly IProjectRepository _projects;
    private readonly MembershipService _membershipService;
    private readonly IInvitationEmailSender _email;
    private readonly ILogger<OrganizationMembersService> _logger;

    public OrganizationMembersService(
        IOrganizationRepository organizations,
        IMembershipRepository memberships,
        IInvitationRepository invitations,
        IProjectRepository projects,
        MembershipService membershipService,
        IInvitationEmailSender email,
        ILogger<OrganizationMembersService> logger)
    {
        _organizations = organizations;
        _memberships = memberships;
        _invitations = invitations;
        _projects = projects;
        _membershipService = membershipService;
        _email = email;
        _logger = logger;
    }

    public async Task<Organization> CreateOrganizationAsync(AppUser creator, string name, CancellationToken cancellationToken = default)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? $"{creator.DisplayName}'s organization" : name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            PlanTier = PlanTier.Free,
        };
        var founder = new Membership
        {
            OrganizationId = org.Id,
            UserId = creator.Id,
            Role = MembershipRole.Admin,
            CreatedAt = org.CreatedAt,
            DisplayName = creator.DisplayName,
            Email = creator.Email,
        };
        await _organizations.CreateWithFounderAsync(org, founder, cancellationToken);
        return org;
    }

    public async Task<IReadOnlyList<Membership>> ListMembersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _memberships.ListMembersByOrgAsync(organizationId, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> ListInvitationsAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _invitations.ListByOrgAsync(organizationId, cancellationToken);

    public async Task<Invitation> InviteAsync(Guid organizationId, Guid invitedByUserId, string email, MembershipRole role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("An email address is required.", nameof(email));
        }

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Token = InvitationRepository.NewToken(organizationId),
            Email = email.Trim(),
            Role = role,
            State = InvitationState.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.Add(InvitationLifetime),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = invitedByUserId,
        };
        await _invitations.PutAsync(invitation, cancellationToken);
        return invitation;
    }

    public async Task RevokeInvitationAsync(Guid organizationId, string token, CancellationToken cancellationToken = default)
    {
        var invitation = await _invitations.GetByTokenAsync(token, cancellationToken);
        if (invitation is null || invitation.OrganizationId != organizationId)
        {
            return;
        }

        invitation.State = InvitationState.Revoked;
        await _invitations.PutAsync(invitation, cancellationToken);
    }

    /// <summary>
    /// company-and-domain-launch: re-send the invite email for a still-acceptable invitation. The
    /// invitation row and token are unchanged — only the email goes out again (same best-effort,
    /// never-throws contract as <see cref="SendInvitationEmailAsync"/>). Returns the invitation, or
    /// null when the token does not name an acceptable invitation for this organization.
    /// </summary>
    public async Task<Invitation?> ResendInvitationEmailAsync(Guid organizationId, string token, string organizationName, string acceptUrl, CancellationToken cancellationToken = default)
    {
        var invitation = await _invitations.GetByTokenAsync(token, cancellationToken);
        if (invitation is null || invitation.OrganizationId != organizationId || !invitation.IsAcceptable(DateTimeOffset.UtcNow))
        {
            return null;
        }

        await SendInvitationEmailAsync(invitation, organizationName, acceptUrl, cancellationToken);
        return invitation;
    }

    /// <summary>
    /// company-and-domain-launch: best-effort invite delivery. The invitation row is already
    /// persisted by <see cref="InviteAsync"/>; a provider error here is logged and swallowed so the
    /// invitation stays valid and its accept link (also returned in the API response) still works.
    /// </summary>
    public async Task SendInvitationEmailAsync(Invitation invitation, string organizationName, string acceptUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            await _email.SendAsync(invitation.Email, organizationName, acceptUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "invitation_email_failed org={OrganizationId} to={Email} — invitation remains valid, link is in the API response",
                invitation.OrganizationId, invitation.Email);
        }
    }

    public sealed record AcceptResult(Guid OrganizationId, MembershipRole Role);

    /// <summary>
    /// Accepts <paramref name="token"/> for <paramref name="user"/>: atomically consumes the invite and
    /// creates the membership, then reconciles away an empty auto-created org (design D1a) if the user
    /// still carries one.
    /// </summary>
    public async Task<AcceptResult> AcceptAsync(AppUser user, string token, CancellationToken cancellationToken = default)
    {
        var invitation = await _invitations.GetByTokenAsync(token, cancellationToken)
            ?? throw new InvitationInvalidException("This invitation link is not valid.");

        if (!invitation.IsAcceptable(DateTimeOffset.UtcNow))
        {
            throw new InvitationInvalidException("This invitation is no longer valid.");
        }

        // security-hardening-pre-pilot D2: an invite link is a bearer secret — anyone it is forwarded
        // to, logged in, or leaked to could otherwise accept it. Require the accepting user's
        // provider-verified email to match the invited address. A missing verified email is a
        // non-match, never a bypass. The failure is reported identically to an invalid/expired invite
        // so it discloses nothing about who was invited or whether the token is real.
        if (string.IsNullOrWhiteSpace(user.Email)
            || !string.Equals(user.Email.Trim(), invitation.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationInvalidException("This invitation is no longer valid.");
        }

        var membership = new Membership
        {
            OrganizationId = invitation.OrganizationId,
            UserId = user.Id,
            Role = invitation.Role,
            CreatedAt = DateTimeOffset.UtcNow,
            DisplayName = user.DisplayName,
            Email = user.Email,
        };

        try
        {
            await _invitations.ClaimAsync(invitation, membership, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            // Already claimed, or the user is already a member. Idempotent success if the latter.
            var already = await _memberships.GetAsync(invitation.OrganizationId, user.Id, cancellationToken);
            if (already is null)
            {
                throw new InvitationInvalidException("This invitation has already been used.");
            }
        }

        await ReconcileAutoCreatedOrgAsync(user, cancellationToken);
        return new AcceptResult(invitation.OrganizationId, invitation.Role);
    }

    /// <summary>
    /// design D1a reconcile fallback: if the user still has their signup-time org and it is provably
    /// empty — no projects and they are its only member — delete it and their membership in it.
    /// </summary>
    private async Task ReconcileAutoCreatedOrgAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (user.OrganizationId == Guid.Empty)
        {
            return;
        }

        var projects = await _projects.ListByOrganizationAsync(user.OrganizationId, cancellationToken);
        if (projects.Count > 0)
        {
            return;
        }

        var members = await _memberships.ListMembersByOrgAsync(user.OrganizationId, cancellationToken);
        var others = members.Where(m => m.UserId != user.Id).ToList();
        if (others.Count > 0)
        {
            return;
        }

        await _memberships.DeleteAsync(user.OrganizationId, user.Id, cancellationToken);
        await _organizations.DeleteAsync(user.OrganizationId, cancellationToken);
    }

    public async Task ChangeRoleAsync(Guid organizationId, Guid targetUserId, MembershipRole role, CancellationToken cancellationToken = default)
    {
        var membership = await _memberships.GetAsync(organizationId, targetUserId, cancellationToken)
            ?? throw new InvitationInvalidException("That person is not a member of this organization.");

        if (membership.Role == role)
        {
            return;
        }

        if (role != MembershipRole.Admin)
        {
            // Demoting to member or viewer — refuse if the target is the only admin.
            await _membershipService.EnsureNotLastAdminAsync(organizationId, targetUserId, cancellationToken);
        }

        membership.Role = role;
        await _memberships.PutAsync(membership, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await _membershipService.EnsureNotLastAdminAsync(organizationId, targetUserId, cancellationToken);
        await _memberships.DeleteAsync(organizationId, targetUserId, cancellationToken);
    }
}

/// <summary>org-membership: an invitation cannot be accepted (missing, expired, revoked, or already used).</summary>
public sealed class InvitationInvalidException : Exception
{
    public InvitationInvalidException(string message) : base(message) { }
}
