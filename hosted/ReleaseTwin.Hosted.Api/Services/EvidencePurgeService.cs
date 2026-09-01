using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// evidence-store spec: deletes every stored evidence document (and its screenshot blobs) whose age
/// since upload exceeds its project's current retention window. Never touches the metadata report the
/// evidence was attached to. Runs once a day as a scheduled Lambda task (RELEASETWIN_LAMBDA_TASK),
/// the same host pattern as <see cref="StalenessDigestService"/>.
/// </summary>
public sealed class EvidencePurgeService
{
    private readonly IRunEvidenceRepository _evidence;
    private readonly IEvidenceBlobStore _blobs;
    private readonly IProjectRepository _projects;
    private readonly ILogger<EvidencePurgeService> _logger;
    private readonly IShareLinkRepository? _shareLinks;

    public EvidencePurgeService(
        IRunEvidenceRepository evidence,
        IEvidenceBlobStore blobs,
        IProjectRepository projects,
        ILogger<EvidencePurgeService> logger,
        IShareLinkRepository? shareLinks = null)
    {
        _evidence = evidence;
        _blobs = blobs;
        _projects = projects;
        _logger = logger;
        // evidence-sharing: optional so the pre-existing purge unit tests still construct this directly.
        _shareLinks = shareLinks;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var all = await _evidence.ListAllAsync(cancellationToken);
        var retentionByProject = (await _projects.ListAllAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => p.EvidenceRetentionDays);

        var purged = 0;
        foreach (var evidence in all)
        {
            var retentionDays = retentionByProject.TryGetValue(evidence.ProjectId, out var days)
                ? days
                : Data.Entities.Project.DefaultEvidenceRetentionDays;

            if (evidence.UploadedAt.AddDays(retentionDays) > now)
            {
                continue;
            }

            foreach (var screenshotId in evidence.ScreenshotIds)
            {
                await _blobs.DeleteAsync(evidence.ProjectId, screenshotId, cancellationToken);
            }

            await _evidence.DeleteAsync(evidence.ProjectId, evidence.ReportId, cancellationToken);

            // evidence-sharing: a purged run's share links must stop resolving.
            if (_shareLinks is not null)
            {
                await _shareLinks.DeleteAllForReportAsync(evidence.ReportId, cancellationToken);
            }

            purged++;
        }

        _logger.LogInformation("evidence_purge_run purged_count={PurgedCount}", purged);
        return purged;
    }
}
