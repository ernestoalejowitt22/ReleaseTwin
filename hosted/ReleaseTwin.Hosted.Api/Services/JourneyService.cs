using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// hosted-journeys: a journey's versions are immutable and sequentially numbered starting at 1.
/// Version numbering is computed here (not in the repository) so both the builder's "save" and any
/// future programmatic writer go through the same next-version logic.
/// </summary>
public sealed class JourneyService
{
    private readonly IJourneyRepository _journeys;
    private readonly IJourneyVersionRepository _versions;

    public JourneyService(IJourneyRepository journeys, IJourneyVersionRepository versions)
    {
        _journeys = journeys;
        _versions = versions;
    }

    public async Task<bool> ProjectOwnsJourneyAsync(Guid projectId, Guid journeyId, CancellationToken cancellationToken = default) =>
        await _journeys.GetAsync(projectId, journeyId, cancellationToken) is not null;

    public Task<Journey> CreateJourneyAsync(Guid projectId, string name, CancellationToken cancellationToken = default) =>
        _journeys.CreateAsync(projectId, name, cancellationToken);

    public Task<Journey?> GetJourneyAsync(Guid projectId, Guid journeyId, CancellationToken cancellationToken = default) =>
        _journeys.GetAsync(projectId, journeyId, cancellationToken);

    public Task<IReadOnlyList<Journey>> ListJourneysAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _journeys.ListByProjectAsync(projectId, cancellationToken);

    /// <summary>
    /// Assigns the next sequential version number and creates it. Not retried on a concurrent-save
    /// race (the conditional write still prevents a silent overwrite — it throws instead) since a
    /// single journey being edited by two people at once is not a case this pass optimizes for.
    /// </summary>
    public async Task<JourneyVersion> CreateVersionAsync(Guid journeyId, string yamlContent, string createdByUserId, string createdByDisplayName, CancellationToken cancellationToken = default)
    {
        var existing = await _versions.ListByJourneyAsync(journeyId, cancellationToken);
        var nextVersion = existing.Count == 0 ? 1 : existing.Max(v => v.Version) + 1;
        return await _versions.CreateAsync(journeyId, nextVersion, yamlContent, createdByUserId, createdByDisplayName, cancellationToken);
    }

    public Task<JourneyVersion?> GetVersionAsync(Guid journeyId, int version, CancellationToken cancellationToken = default) =>
        _versions.GetAsync(journeyId, version, cancellationToken);

    /// <summary>Most recent first, for the version-history view.</summary>
    public async Task<IReadOnlyList<JourneyVersion>> ListVersionHistoryAsync(Guid journeyId, CancellationToken cancellationToken = default)
    {
        var versions = await _versions.ListByJourneyAsync(journeyId, cancellationToken);
        return versions.OrderByDescending(v => v.Version).ToList();
    }
}
