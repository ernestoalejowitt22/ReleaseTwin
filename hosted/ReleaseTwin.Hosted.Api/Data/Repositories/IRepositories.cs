using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<AppUser?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates the organization and user together, atomically. Throws <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/> if a user with this ClerkUserId was created concurrently — callers should re-read via <see cref="GetByClerkUserIdAsync"/> instead of retrying the create.</summary>
    Task CreateWithOrganizationAsync(Organization organization, AppUser user, CancellationToken cancellationToken = default);
}

public interface IProjectRepository
{
    Task<Project> CreateAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsInOrganizationAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);
}

public interface IApiTokenRepository
{
    Task<ApiToken> CreateAsync(Guid projectId, Guid organizationId, string tokenHash, string displayPrefix, CancellationToken cancellationToken = default);
    Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiToken>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default);
}

/// <summary>Keyed by ProjectId alone — see design.md's revised Connection key note: nesting under the org partition would need OrganizationId threaded through call sites that only naturally have ProjectId, for no real access pattern this codebase has (there's no "list connections by org").</summary>
public interface IConnectionRepository
{
    Task<Connection?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task UpsertAsync(Guid projectId, string provider, string externalRepo, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface ICaseReportRepository
{
    Task AddAsync(UploadedCaseReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadedCaseReport>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IFlagProofReportRepository
{
    Task AddAsync(UploadedFlagProofReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadedFlagProofReport>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IUsageCounterRepository
{
    /// <summary>Atomically increments the counter for (organizationId, current period) — safe under concurrent ingest requests.</summary>
    Task IncrementAsync(Guid organizationId, DateOnly period, bool isFlagProof, CancellationToken cancellationToken = default);

    /// <summary>Returns a zero-valued counter (never null) if nothing has been uploaded yet this period.</summary>
    Task<UsageCounter> GetAsync(Guid organizationId, DateOnly period, CancellationToken cancellationToken = default);
}
