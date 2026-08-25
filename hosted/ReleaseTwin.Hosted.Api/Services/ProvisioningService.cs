using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// account-provisioning: signup requires no human approval, organization/project creation is
/// self-serve, and API tokens are self-serve issued/revoked and project-scoped.
/// </summary>
public sealed class ProvisioningService
{
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly IApiTokenRepository _tokens;
    private readonly ITokenService _tokenService;

    public ProvisioningService(IUserRepository users, IProjectRepository projects, IApiTokenRepository tokens, ITokenService tokenService)
    {
        _users = users;
        _projects = projects;
        _tokens = tokens;
        _tokenService = tokenService;
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

    public async Task<Project> CreateProjectAsync(Guid organizationId, string name, CancellationToken cancellationToken = default) =>
        await _projects.CreateAsync(organizationId, name, cancellationToken);

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
