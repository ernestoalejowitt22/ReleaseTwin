using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>plan-tier-gating: read, mutate, re-put — same pattern as ApiTokenRepository.RevokeAsync.</summary>
    Task SetPlanTierAsync(Guid organizationId, PlanTier tier, CancellationToken cancellationToken = default);
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

    /// <summary>operator-alerting design.md: every project across every organization, for the daily staleness digest — the one caller with no natural organization partition key to scope a Query to. Backed by a full-table Scan (see IHostedTable.ScanByEntityTypeAsync); not for use on any per-request path.</summary>
    Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>evidence-store: set this project's evidence capture default and retention window. Caller validates the window against <see cref="Project.MaxEvidenceRetentionDays"/>.</summary>
    Task SetEvidenceConfigAsync(Guid organizationId, Guid projectId, bool captureDefault, int retentionDays, CancellationToken cancellationToken = default);
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

    /// <summary>trend-analytics: reports uploaded in the half-open window <c>[from, to)</c> for one project — a native project-partition range Query, no GSI.</summary>
    Task<IReadOnlyList<UploadedCaseReport>> ListByProjectInRangeAsync(Guid projectId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public interface IFlagProofReportRepository
{
    Task AddAsync(UploadedFlagProofReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadedFlagProofReport>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>trend-analytics: reports uploaded in the half-open window <c>[from, to)</c> for one project.</summary>
    Task<IReadOnlyList<UploadedFlagProofReport>> ListByProjectInRangeAsync(Guid projectId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public interface IRunEvidenceRepository
{
    Task AddAsync(UploadedRunEvidence evidence, CancellationToken cancellationToken = default);

    /// <summary>The single evidence document stored for one report, or null. Scoped to the project it belongs to.</summary>
    Task<UploadedRunEvidence?> GetByReportAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadedRunEvidence>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>evidence-store purge: every stored evidence document across every project. Full-table scan, purge-job only.</summary>
    Task<IReadOnlyList<UploadedRunEvidence>> ListAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default);
}

public interface IJourneyRepository
{
    Task<Journey> CreateAsync(Guid projectId, string name, CancellationToken cancellationToken = default);
    Task<Journey?> GetAsync(Guid projectId, Guid journeyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Journey>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IJourneyVersionRepository
{
    /// <summary>Throws <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/> if <paramref name="version"/> already exists for this journey — callers assign the version number, this never silently overwrites one.</summary>
    Task<JourneyVersion> CreateAsync(Guid journeyId, int version, string yamlContent, string createdByUserId, string createdByDisplayName, CancellationToken cancellationToken = default);
    Task<JourneyVersion?> GetAsync(Guid journeyId, int version, CancellationToken cancellationToken = default);

    /// <summary>Ordered oldest to newest.</summary>
    Task<IReadOnlyList<JourneyVersion>> ListByJourneyAsync(Guid journeyId, CancellationToken cancellationToken = default);
}

public interface IAdapterCredentialRepository
{
    /// <summary>Upserts in place — rotation replaces the stored value entirely, no history kept.</summary>
    Task<AdapterCredential> SetAsync(Guid projectId, string adapter, string encryptedFields, string lastSetByUserId, string lastSetByDisplayName, CancellationToken cancellationToken = default);
    Task<AdapterCredential?> GetAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdapterCredential>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default);
}

public interface IProjectSecretRepository
{
    /// <summary>Upserts in place — rotation replaces the stored value entirely, no history kept.</summary>
    Task<ProjectSecret> SetAsync(Guid projectId, string name, string encryptedValue, string lastSetByUserId, string lastSetByDisplayName, CancellationToken cancellationToken = default);
    Task<ProjectSecret?> GetAsync(Guid projectId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectSecret>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, string name, CancellationToken cancellationToken = default);
}

public interface IUsageCounterRepository
{
    /// <summary>Atomically increments the counter for (organizationId, current period) — safe under concurrent ingest requests.</summary>
    Task IncrementAsync(Guid organizationId, DateOnly period, bool isFlagProof, CancellationToken cancellationToken = default);

    /// <summary>Returns a zero-valued counter (never null) if nothing has been uploaded yet this period.</summary>
    Task<UsageCounter> GetAsync(Guid organizationId, DateOnly period, CancellationToken cancellationToken = default);
}
