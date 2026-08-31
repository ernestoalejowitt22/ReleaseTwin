using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>plan-tier-gating: read, mutate, re-put — same pattern as ApiTokenRepository.RevokeAsync.</summary>
    Task SetPlanTierAsync(Guid organizationId, PlanTier tier, CancellationToken cancellationToken = default);

    /// <summary>billing: set the org's Merchant-of-Record linkage + status in a single write. All writes are "set to state X" so a redelivered webhook event replays safely.</summary>
    Task SetBillingAsync(Guid organizationId, BillingStatus status, DateTimeOffset since, BillingCadence? cadence, string? customerId, string? subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>billing: every organization, for the nightly reconciliation job. Full-table Scan — never a per-request path.</summary>
    Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>onboarding-activation: idempotently marks the org as having ingested a real run. A no-op
    /// (no write) once it is already set, so the extra read is the only cost on the ingest path after
    /// the first run.</summary>
    Task MarkIngestedRealRunAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>org-membership: removes the organization item. Used only by the invite-accept reconcile
    /// path to clean up an auto-created org that is provably empty (no projects, sole member).</summary>
    Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>org-membership: creates an additional organization and its founding admin membership
    /// atomically (the creating user already exists).</summary>
    Task CreateWithFounderAsync(Organization organization, Membership founder, CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<AppUser?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates the organization and user together, atomically. Throws <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/> if a user with this ClerkUserId was created concurrently — callers should re-read via <see cref="GetByClerkUserIdAsync"/> instead of retrying the create.</summary>
    Task CreateWithOrganizationAsync(Organization organization, AppUser user, CancellationToken cancellationToken = default);

    /// <summary>org-membership: creates the organization, the user, and the user's founding admin
    /// <see cref="Membership"/> together, atomically. Same concurrency contract as
    /// <see cref="CreateWithOrganizationAsync"/>.</summary>
    Task CreateWithOrganizationAsync(Organization organization, AppUser user, Membership foundingMembership, CancellationToken cancellationToken = default);

    /// <summary>org-membership: persists a user with no new organization — used when signup follows an
    /// accepted invitation. Same concurrency contract as <see cref="CreateWithOrganizationAsync"/>.</summary>
    Task CreateAsync(AppUser user, CancellationToken cancellationToken = default);
}

/// <summary>org-membership: the many-to-many link between users and organizations. Membership items live
/// under the org partition; the reverse "orgs for a user" lookup rides the overloaded GSI1.</summary>
public interface IMembershipRepository
{
    Task<IReadOnlyList<Membership>> ListMembersByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> ListOrgsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Membership?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task PutAsync(Membership membership, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>org-membership: outstanding invitations to join an organization. Single-use consumption is
/// atomic — see <see cref="ClaimAsync"/>.</summary>
public interface IInvitationRepository
{
    Task PutAsync(Invitation invitation, CancellationToken cancellationToken = default);

    /// <summary>Locates the invitation from the token alone (the token encodes the org id as its prefix).</summary>
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invitation>> ListByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes <paramref name="invitation"/> and creates <paramref name="membership"/> in one
    /// all-or-nothing write: a claim marker guarantees the invite is used at most once, and the membership
    /// write guards against a duplicate join. Throws
    /// <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/> if the invite was already
    /// claimed or the user is already a member. On success the invitation is also flipped to
    /// <see cref="InvitationState.Accepted"/> for listings.
    /// </summary>
    Task ClaimAsync(Invitation invitation, Membership membership, CancellationToken cancellationToken = default);
}

public interface IProjectRepository
{
    Task<Project> CreateAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsInOrganizationAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>billing: removes the project item. Evidence and reports under it are left in place (reachable only via the project key) — a downgrade never deletes evidence, and a hard project delete is rare.</summary>
    Task DeleteAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);

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

/// <summary>evidence-sharing: per-run revocable read-only share links. Keyed by the token hash for O(1)
/// resolution; the token carries the report id as its prefix so the item can be located from the
/// token alone.</summary>
public interface IShareLinkRepository
{
    Task PutAsync(ShareLink link, CancellationToken cancellationToken = default);
    Task<ShareLink?> GetByTokenHashAsync(Guid reportId, string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareLink>> ListByReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Revokes one link (by its <see cref="ShareLink.Id"/>). No-op if it is not found under this report.</summary>
    Task RevokeAsync(Guid reportId, Guid linkId, CancellationToken cancellationToken = default);

    /// <summary>evidence-sharing: hard-delete every share link for a report — called by the evidence purge.</summary>
    Task DeleteAllForReportAsync(Guid reportId, CancellationToken cancellationToken = default);
}

/// <summary>run-notifications: a project's outbound notification targets (Slack / generic webhook).</summary>
public interface INotificationTargetRepository
{
    Task<IReadOnlyList<NotificationTarget>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<NotificationTarget?> GetAsync(Guid projectId, Guid targetId, CancellationToken cancellationToken = default);
    Task PutAsync(NotificationTarget target, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>Records the outcome of a delivery attempt (read-mutate-put). A no-op if the target was deleted meanwhile.</summary>
    Task RecordOutcomeAsync(Guid projectId, Guid targetId, string outcome, DateTimeOffset attemptedAt, CancellationToken cancellationToken = default);
}

public interface IUsageCounterRepository
{
    /// <summary>Atomically increments the counter for (organizationId, current period) — safe under concurrent ingest requests.</summary>
    Task IncrementAsync(Guid organizationId, DateOnly period, bool isFlagProof, CancellationToken cancellationToken = default);

    /// <summary>Returns a zero-valued counter (never null) if nothing has been uploaded yet this period.</summary>
    Task<UsageCounter> GetAsync(Guid organizationId, DateOnly period, CancellationToken cancellationToken = default);
}
