using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record DashboardProjectSummary(Guid Id, string Name, bool ReadOnly = false);

public sealed record DashboardConnectionView(string Provider, string ExternalRepo, DateTimeOffset ConnectedAt);

public sealed record DashboardTokenView(Guid Id, string DisplayPrefix, DateTimeOffset CreatedAt, bool IsRevoked);

public sealed record DashboardCaseReportView(string CaseId, bool Passed, string? Classification, string CleanupStatus, DateTimeOffset UploadedAt, Guid ReportId, string EvidenceStatus);

public sealed record DashboardFlagProofReportView(string CaseId, string BuildIdentity, string Outcome, bool? KnownBadLegPassed, bool? KnownGoodLegPassed, DateTimeOffset UploadedAt, Guid ReportId, string EvidenceStatus);

/// <summary>usage-metering: organization-wide report counts for the current period, independent of which project is selected — see dashboard spec's "Dashboard shows the organization's current usage".</summary>
public sealed record DashboardUsageSummary(int CaseReportCount, int FlagProofReportCount, DateOnly PeriodStart);

public sealed record DashboardView(
    IReadOnlyList<DashboardProjectSummary> Projects,
    DashboardProjectSummary? SelectedProject,
    DashboardConnectionView? Connection,
    IReadOnlyList<DashboardTokenView> Tokens,
    IReadOnlyList<DashboardCaseReportView> CaseReports,
    IReadOnlyList<DashboardFlagProofReportView> FlagProofReports,
    DashboardUsageSummary Usage,
    PlanTier PlanTier,
    Entitlements Entitlements,
    bool IsSelectedProjectStale,
    BillingStatus BillingStatus = BillingStatus.Active,
    BillingCadence? BillingCadence = null,
    bool HasBillingLinkage = false,
    bool HasReadOnlyProjects = false,
    bool BillingEnabled = false);

/// <summary>
/// hosted-react-frontend: the data-shaping half of what was Dashboard.cshtml.cs's OnGetAsync,
/// extracted so it's directly unit-testable (same pattern DashboardModelTests already used) rather
/// than living inline in a minimal-API lambda. dashboard spec: "A customer sees only their own
/// organization's data" — every query here filters by the org id the caller supplies, never a
/// client-chosen value.
/// </summary>
public sealed class DashboardService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly IConnectionRepository _connections;
    private readonly IApiTokenRepository _tokens;
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IUsageCounterRepository _usage;
    private readonly IRunEvidenceRepository _runEvidence;
    private readonly IEntitlementService _entitlements;
    private readonly PolarOptions _polarOptions;

    public DashboardService(
        IOrganizationRepository organizations,
        IProjectRepository projects,
        IConnectionRepository connections,
        IApiTokenRepository tokens,
        ICaseReportRepository caseReports,
        IFlagProofReportRepository flagProofReports,
        IUsageCounterRepository usage,
        IRunEvidenceRepository runEvidence,
        IEntitlementService entitlements,
        PolarOptions? polarOptions = null)
    {
        _polarOptions = polarOptions ?? new PolarOptions();
        _organizations = organizations;
        _projects = projects;
        _connections = connections;
        _tokens = tokens;
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _usage = usage;
        _runEvidence = runEvidence;
        _entitlements = entitlements;
    }

    public async Task<DashboardView> GetDashboardViewAsync(Guid organizationId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var planTier = organization?.PlanTier ?? PlanTier.Free;
        var entitlements = _entitlements.For(organization);
        var projects = await _projects.ListByOrganizationAsync(organizationId, cancellationToken);

        // A project only ever resolves if it belongs to the caller's own organization — the source
        // of the "cross-organization data is never shown" guarantee.
        var selectedProject = projectId is null
            ? projects.FirstOrDefault()
            : projects.FirstOrDefault(p => p.Id == projectId);

        // billing (D5): projects beyond the org's effective tier limit (after a downgrade / cancel)
        // are read-only — still listed with their evidence, but ingest is blocked. Same resolver the
        // ingest path uses, so the two never disagree.
        var writableProjectIds = ProjectWritabilityService.WritableProjectIds(projects, entitlements.MaxProjects);
        bool IsReadOnly(Guid id) => !writableProjectIds.Contains(id);

        var projectSummaries = projects.Select(p => new DashboardProjectSummary(p.Id, p.Name, IsReadOnly(p.Id))).ToList();
        var hasReadOnlyProjects = projectSummaries.Any(p => p.ReadOnly);

        // usage-metering: org-wide, independent of selectedProject — see design.md's explicit
        // "org-wide, not per-project" decision. Computed even if no project is selected.
        var counter = await _usage.GetAsync(organizationId, Keys.CurrentUtcPeriod(), cancellationToken);
        var usage = new DashboardUsageSummary((int)counter.CaseReportCount, (int)counter.FlagProofReportCount, counter.PeriodStart);

        var billingStatus = organization?.BillingStatus ?? BillingStatus.Active;
        var billingCadence = organization?.BillingCadence;
        var hasBillingLinkage = organization?.PolarSubscriptionId is not null;
        var billingEnabled = _polarOptions.IsUpgradeEnabled;

        if (selectedProject is null)
        {
            return new DashboardView(projectSummaries, null, null, [], [], [], usage, planTier, entitlements, IsSelectedProjectStale: false,
                billingStatus, billingCadence, hasBillingLinkage, hasReadOnlyProjects, billingEnabled);
        }

        var connection = await _connections.GetAsync(selectedProject.Id, cancellationToken);
        var tokens = await _tokens.ListByProjectAsync(selectedProject.Id, cancellationToken);
        var caseReports = await _caseReports.ListByProjectAsync(selectedProject.Id, cancellationToken);
        var flagProofReports = await _flagProofReports.ListByProjectAsync(selectedProject.Id, cancellationToken);

        // evidence-store: per-report evidence state for the dashboard drill-down.
        var evidenceEntitled = entitlements.EvidenceViewer;
        var evidenceByReport = evidenceEntitled
            ? (await _runEvidence.ListByProjectAsync(selectedProject.Id, cancellationToken)).ToDictionary(e => e.ReportId)
            : new Dictionary<Guid, Data.Entities.UploadedRunEvidence>();
        var now = DateTimeOffset.UtcNow;

        string EvidenceStatus(Guid reportId)
        {
            if (!evidenceEntitled)
            {
                return "not-entitled";
            }

            if (!evidenceByReport.TryGetValue(reportId, out var evidence))
            {
                return "none";
            }

            return evidence.UploadedAt.AddDays(selectedProject.EvidenceRetentionDays) < now ? "expired" : "available";
        }

        // upload-staleness spec: judged from this project's own combined upload timeline, not
        // case reports or flag-proof reports alone.
        var uploadTimestamps = caseReports.Select(r => r.UploadedAt)
            .Concat(flagProofReports.Select(r => r.UploadedAt))
            .ToList();
        var isStale = UploadStalenessCalculator.IsStale(uploadTimestamps, DateTimeOffset.UtcNow);

        return new DashboardView(
            projectSummaries,
            new DashboardProjectSummary(selectedProject.Id, selectedProject.Name, IsReadOnly(selectedProject.Id)),
            connection is null ? null : new DashboardConnectionView(connection.Provider, connection.ExternalRepo, connection.ConnectedAt),
            tokens.Select(t => new DashboardTokenView(t.Id, t.DisplayPrefix, t.CreatedAt, t.IsRevoked)).ToList(),
            caseReports.Select(r => new DashboardCaseReportView(r.CaseId, r.Passed, r.Classification, r.CleanupStatus, r.UploadedAt, r.Id, EvidenceStatus(r.Id))).ToList(),
            flagProofReports.Select(r => new DashboardFlagProofReportView(r.CaseId, r.BuildIdentity, r.Outcome, r.KnownBadLegPassed, r.KnownGoodLegPassed, r.UploadedAt, r.Id, EvidenceStatus(r.Id))).ToList(),
            usage,
            planTier,
            entitlements,
            isStale,
            billingStatus,
            billingCadence,
            hasBillingLinkage,
            hasReadOnlyProjects,
            billingEnabled);
    }
}
