using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// account-provisioning: signup requires no human approval, organization/project creation is
/// self-serve, and API tokens are self-serve issued/revoked and project-scoped.
/// </summary>
public sealed class ProvisioningService
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly IApiTokenRepository _tokens;
    private readonly ITokenService _tokenService;
    private readonly IEntitlementService _entitlements;
    private readonly IPolarClient _polar;
    private readonly ILogger<ProvisioningService> _logger;
    private readonly IInvitationRepository? _invitations;

    public ProvisioningService(IUserRepository users, IOrganizationRepository organizations, IProjectRepository projects, IApiTokenRepository tokens, ITokenService tokenService, IEntitlementService entitlements, IPolarClient? polar = null, ILogger<ProvisioningService>? logger = null, IInvitationRepository? invitations = null)
    {
        _users = users;
        _organizations = organizations;
        _projects = projects;
        _tokens = tokens;
        _tokenService = tokenService;
        _entitlements = entitlements;
        // billing: DI always supplies these; the null fallbacks keep the many unit tests that construct
        // this service directly (and never touch a paid org) compiling without a Polar fake each.
        _polar = polar ?? NullPolarClient.Instance;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProvisioningService>.Instance;
        // org-membership: optional so the pre-membership unit tests still construct this directly.
        _invitations = invitations;
    }

    /// <summary>
    /// First login auto-creates a personal organization so the account is immediately usable — no
    /// separate "create org" step required to satisfy the self-serve requirement.
    ///
    /// org-membership (design D1a): when <paramref name="pendingInviteToken"/> names an acceptable
    /// invitation whose email matches (or when the caller carries no email), the user is created with
    /// no organization — they are only signing up to join an existing one, and creating a throwaway
    /// org for them would leave an empty shell. The reconcile path in
    /// <see cref="OrganizationMembersService.AcceptAsync"/> covers the case where the token was not
    /// forwarded.
    /// </summary>
    public async Task<AppUser> GetOrCreateUserAsync(string clerkUserId, string displayName, string? email, string? pendingInviteToken = null, CancellationToken cancellationToken = default)
    {
        var existing = await _users.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (await IsJoiningByInviteAsync(pendingInviteToken, email, cancellationToken))
        {
            var invitedUser = new AppUser
            {
                Id = Guid.NewGuid(),
                ClerkUserId = clerkUserId,
                DisplayName = displayName,
                Email = email,
                CreatedAt = DateTimeOffset.UtcNow,
                OrganizationId = Guid.Empty,
            };

            try
            {
                await _users.CreateAsync(invitedUser, cancellationToken);
                return invitedUser;
            }
            catch (ConditionalCheckFailedException)
            {
                return await _users.GetByClerkUserIdAsync(clerkUserId, cancellationToken)
                    ?? throw new InvalidOperationException("Conditional create failed but no user was found on re-read.");
            }
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = $"{displayName}'s organization",
            CreatedAt = DateTimeOffset.UtcNow,
            PlanTier = PlanTier.Free,
        };

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            ClerkUserId = clerkUserId,
            DisplayName = displayName,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
            OrganizationId = organization.Id,
        };

        var founding = new Membership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = MembershipRole.Admin,
            CreatedAt = user.CreatedAt,
            DisplayName = displayName,
            Email = email,
        };

        try
        {
            await _users.CreateWithOrganizationAsync(organization, user, founding, cancellationToken);
            return user;
        }
        catch (ConditionalCheckFailedException)
        {
            // usage-metering design.md: a concurrent request already created this user — re-read
            // instead of retrying the create, exactly mirroring the prior EF Core "check then create"
            // shape but now race-safe by construction.
            return await _users.GetByClerkUserIdAsync(clerkUserId, cancellationToken)
                ?? throw new InvalidOperationException("Conditional create failed but no user was found on re-read.");
        }
    }

    private async Task<bool> IsJoiningByInviteAsync(string? pendingInviteToken, string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pendingInviteToken) || _invitations is null)
        {
            return false;
        }

        var invite = await _invitations.GetByTokenAsync(pendingInviteToken, cancellationToken);
        if (invite is null || !invite.IsAcceptable(DateTimeOffset.UtcNow))
        {
            return false;
        }

        return email is null || string.Equals(email, invite.Email, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// plan-tier-gating: the project cap comes from the organization tier's <c>maxProjects</c>
    /// entitlement (null = unlimited). Reads the organization and, only when a cap applies, its
    /// current project count before creating — extra reads on an already low-frequency operation
    /// (project creation, not ingest), avoiding a maintained counter that could drift from the
    /// actual project list (design.md).
    /// </summary>
    public async Task<Project> CreateProjectAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot create a project: organization {organizationId} not found.");

        var maxProjects = _entitlements.For(organization).MaxProjects;
        var hasSubscription = !string.IsNullOrEmpty(organization.PolarSubscriptionId);

        int? currentCount = null;
        if (maxProjects is not null || hasSubscription)
        {
            currentCount = (await _projects.ListByOrganizationAsync(organizationId, cancellationToken)).Count;
        }

        if (maxProjects is not null && currentCount >= maxProjects.Value)
        {
            throw new ProjectLimitExceededException(
                $"The {organization.PlanTier} tier is limited to {maxProjects.Value} project(s). Upgrade to create more.");
        }

        // billing (design.md D6): for a paying org, raise the Merchant-of-Record quantity BEFORE
        // creating. A rejection (declined proration charge, API error) fails the creation closed with
        // a portal-pointing message — nothing is created.
        if (hasSubscription)
        {
            try
            {
                await _polar.SetSubscriptionQuantityAsync(organization.PolarSubscriptionId!, currentCount!.Value + 1, cancellationToken);
            }
            catch (PolarException ex)
            {
                _logger.LogWarning(ex, "Polar rejected the quantity increase creating a project for org {OrgId}.", organizationId);
                throw new EntitlementRequiredException(
                    "billing",
                    "Your payment provider rejected the charge for an additional project. Update your payment method in the billing portal, then try again.");
            }
        }

        return await _projects.CreateAsync(organizationId, name, cancellationToken);
    }

    /// <summary>
    /// billing (design.md D6): deleting a project lowers the Merchant-of-Record quantity best-effort —
    /// a failure is logged and swallowed, never blocking the delete; the nightly reconciliation job
    /// closes any resulting drift.
    /// </summary>
    public async Task DeleteProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot delete a project: organization {organizationId} not found.");

        await _projects.DeleteAsync(organizationId, projectId, cancellationToken);

        if (!string.IsNullOrEmpty(organization.PolarSubscriptionId))
        {
            try
            {
                var remaining = (await _projects.ListByOrganizationAsync(organizationId, cancellationToken)).Count;
                await _polar.SetSubscriptionQuantityAsync(organization.PolarSubscriptionId!, remaining, cancellationToken);
            }
            catch (PolarException ex)
            {
                _logger.LogWarning(ex, "Failed to lower Polar quantity after deleting project {ProjectId} for org {OrgId}; leaving for reconciliation.", projectId, organizationId);
            }
        }
    }

    /// <summary>
    /// plan-catalog-and-entitlements: the single tier-mutation point. A future billing webhook calls
    /// this; for now it is reached only by the payment-free self-serve upgrade
    /// (<see cref="UpgradeToTeamAsync"/>) and by an operator setting Enterprise out-of-band.
    /// </summary>
    public Task SetTierAsync(Guid organizationId, PlanTier tier, CancellationToken cancellationToken = default) =>
        _organizations.SetPlanTierAsync(organizationId, tier, cancellationToken);

    /// <summary>plan-tier-gating: self-serve Free → Team. No payment collected — an explicit placeholder for the eventual real paid flow. Enterprise is deliberately not reachable this way.</summary>
    public Task UpgradeToTeamAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        SetTierAsync(organizationId, PlanTier.Team, cancellationToken);

    /// <summary>
    /// Returns the raw token value once — it is never retrievable again after this call.
    /// Takes <paramref name="organizationId"/> explicitly (rather than looking the project up by id
    /// alone) because Project's primary key is (OrganizationId, ProjectId) by design (design.md) —
    /// every caller already knows the organization at this point (it's what authorized the request in
    /// the first place), and denormalizing it onto the token here is what lets IngestEndpoints
    /// increment the right organization's usage counter later without an extra read on the ingest hot
    /// path (design.md's OrganizationId denormalization decision).
    /// </summary>
    public async Task<(ApiToken Token, string RawValue)> IssueTokenAsync(Guid projectId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var generated = _tokenService.GenerateToken();
        var token = await _tokens.CreateAsync(projectId, organizationId, generated.Hash, generated.DisplayPrefix, cancellationToken);
        return (token, generated.RawValue);
    }

    public async Task RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
        await _tokens.RevokeAsync(tokenId, cancellationToken);
}
