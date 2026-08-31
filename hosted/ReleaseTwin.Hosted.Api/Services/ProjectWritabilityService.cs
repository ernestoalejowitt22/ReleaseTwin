using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// plan-tier-gating / billing (D5): the single place that decides which of an organization's projects
/// are writable after a downgrade or cancellation left it with more projects than its effective tier
/// allows. The oldest projects up to the limit stay writable; the rest are read-only — still listed
/// with their evidence, but rejecting new ingest. Ingest and the dashboard both resolve through here
/// so they never disagree.
/// </summary>
public sealed class ProjectWritabilityService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly IEntitlementService _entitlements;

    public ProjectWritabilityService(IOrganizationRepository organizations, IProjectRepository projects, IEntitlementService entitlements)
    {
        _organizations = organizations;
        _projects = projects;
        _entitlements = entitlements;
    }

    /// <summary>
    /// The writable subset of <paramref name="projects"/> given the effective project cap
    /// (<c>null</c> ⇒ all writable). Ordering is oldest-by-creation, id as a stable tie-break, so a
    /// reconciliation run and a dashboard render always pick the same projects.
    /// </summary>
    public static IReadOnlySet<Guid> WritableProjectIds(IEnumerable<Project> projects, int? maxProjects)
    {
        if (maxProjects is null)
        {
            return projects.Select(p => p.Id).ToHashSet();
        }

        return projects
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(maxProjects.Value)
            .Select(p => p.Id)
            .ToHashSet();
    }

    /// <summary>The effective project cap for an org — the tier's <c>maxProjects</c> after the billing-status modifier.</summary>
    public int? EffectiveMaxProjects(Organization? organization) => _entitlements.For(organization).MaxProjects;

    public async Task<IReadOnlySet<Guid>> WritableProjectIdsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var projects = await _projects.ListByOrganizationAsync(organizationId, cancellationToken);
        return WritableProjectIds(projects, EffectiveMaxProjects(organization));
    }

    /// <summary>Whether new evidence ingest is allowed for this project right now.</summary>
    public async Task<bool> IsWritableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default) =>
        (await WritableProjectIdsAsync(organizationId, cancellationToken)).Contains(projectId);
}
