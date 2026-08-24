using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record DashboardProjectSummary(Guid Id, string Name);

public sealed record DashboardConnectionView(string Provider, string ExternalRepo, DateTimeOffset ConnectedAt);

public sealed record DashboardTokenView(Guid Id, string DisplayPrefix, DateTimeOffset CreatedAt, bool IsRevoked);

public sealed record DashboardCaseReportView(string CaseId, bool Passed, string? Classification, string CleanupStatus, DateTimeOffset UploadedAt);

public sealed record DashboardFlagProofReportView(string CaseId, string BuildIdentity, string Outcome, bool? KnownBadLegPassed, bool? KnownGoodLegPassed, DateTimeOffset UploadedAt);

public sealed record DashboardView(
    IReadOnlyList<DashboardProjectSummary> Projects,
    DashboardProjectSummary? SelectedProject,
    DashboardConnectionView? Connection,
    IReadOnlyList<DashboardTokenView> Tokens,
    IReadOnlyList<DashboardCaseReportView> CaseReports,
    IReadOnlyList<DashboardFlagProofReportView> FlagProofReports);

/// <summary>
/// hosted-react-frontend: the data-shaping half of what was Dashboard.cshtml.cs's OnGetAsync,
/// extracted so it's directly unit-testable (same pattern DashboardModelTests already used) rather
/// than living inline in a minimal-API lambda. dashboard spec: "A customer sees only their own
/// organization's data" — every query here filters by the org id the caller supplies, never a
/// client-chosen value.
/// </summary>
public sealed class DashboardService
{
    private readonly HostedDbContext _db;

    public DashboardService(HostedDbContext db) => _db = db;

    public async Task<DashboardView> GetDashboardViewAsync(Guid organizationId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var projects = await _db.Projects.Where(p => p.OrganizationId == organizationId).OrderBy(p => p.Name).ToListAsync(cancellationToken);

        // A project only ever resolves if it belongs to the caller's own organization — the source
        // of the "cross-organization data is never shown" guarantee.
        var selectedProject = projectId is null
            ? projects.FirstOrDefault()
            : projects.FirstOrDefault(p => p.Id == projectId);

        var projectSummaries = projects.Select(p => new DashboardProjectSummary(p.Id, p.Name)).ToList();

        if (selectedProject is null)
        {
            return new DashboardView(projectSummaries, null, null, [], [], []);
        }

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.ProjectId == selectedProject.Id, cancellationToken);
        var tokens = await _db.ApiTokens.Where(t => t.ProjectId == selectedProject.Id).OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
        var caseReports = await _db.UploadedCaseReports.Where(r => r.ProjectId == selectedProject.Id).OrderByDescending(r => r.UploadedAt).ToListAsync(cancellationToken);
        var flagProofReports = await _db.UploadedFlagProofReports.Where(r => r.ProjectId == selectedProject.Id).OrderByDescending(r => r.UploadedAt).ToListAsync(cancellationToken);

        return new DashboardView(
            projectSummaries,
            new DashboardProjectSummary(selectedProject.Id, selectedProject.Name),
            connection is null ? null : new DashboardConnectionView(connection.Provider, connection.ExternalRepo, connection.ConnectedAt),
            tokens.Select(t => new DashboardTokenView(t.Id, t.DisplayPrefix, t.CreatedAt, t.IsRevoked)).ToList(),
            caseReports.Select(r => new DashboardCaseReportView(r.CaseId, r.Passed, r.Classification, r.CleanupStatus, r.UploadedAt)).ToList(),
            flagProofReports.Select(r => new DashboardFlagProofReportView(r.CaseId, r.BuildIdentity, r.Outcome, r.KnownBadLegPassed, r.KnownGoodLegPassed, r.UploadedAt)).ToList());
    }
}
