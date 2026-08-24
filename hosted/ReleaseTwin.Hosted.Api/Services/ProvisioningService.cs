using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// account-provisioning: signup requires no human approval, organization/project creation is
/// self-serve, and API tokens are self-serve issued/revoked and project-scoped.
/// </summary>
public sealed class ProvisioningService
{
    private readonly HostedDbContext _db;
    private readonly ITokenService _tokens;

    public ProvisioningService(HostedDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    /// <summary>First login auto-creates a personal organization so the account is immediately usable — no separate "create org" step required to satisfy the self-serve requirement.</summary>
    public async Task<AppUser> GetOrCreateUserAsync(string clerkUserId, string displayName, string? email, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
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

        _db.Organizations.Add(organization);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        user.Organization = organization;
        return user;
    }

    public async Task<Project> CreateProjectAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            OrganizationId = organizationId,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return project;
    }

    /// <summary>Returns the raw token value once — it is never retrievable again after this call.</summary>
    public async Task<(ApiToken Token, string RawValue)> IssueTokenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var generated = _tokens.GenerateToken();
        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TokenHash = generated.Hash,
            DisplayPrefix = generated.DisplayPrefix,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.ApiTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return (token, generated.RawValue);
    }

    public async Task RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await _db.ApiTokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token is null || token.IsRevoked)
        {
            return;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
