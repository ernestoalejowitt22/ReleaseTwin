using Amazon.DynamoDBv2.Model;
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

    public ProvisioningService(IUserRepository users, IOrganizationRepository organizations, IProjectRepository projects, IApiTokenRepository tokens, ITokenService tokenService, IEntitlementService entitlements)
    {
        _users = users;
        _organizations = organizations;
        _projects = projects;
        _tokens = tokens;
        _tokenService = tokenService;
        _entitlements = entitlements;
    }

    /// <summary>First login auto-creates a personal organization so the account is immediately usable — no separate "create org" step required to satisfy the self-serve requirement.</summary>
    public async Task<AppUser> GetOrCreateUserAsync(string clerkUserId, string displayName, string? email, CancellationToken cancellationToken = default)
    {
        var existing = await _users.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
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

        try
        {
            await _users.CreateWithOrganizationAsync(organization, user, cancellationToken);
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
        if (maxProjects is not null)
        {
            var existingProjects = await _projects.ListByOrganizationAsync(organizationId, cancellationToken);
            if (existingProjects.Count >= maxProjects.Value)
            {
                throw new ProjectLimitExceededException(
                    $"The {organization.PlanTier} tier is limited to {maxProjects.Value} project(s). Upgrade to create more.");
            }
        }

        return await _projects.CreateAsync(organizationId, name, cancellationToken);
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
