using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// operator-alerting design.md: a narrow abstraction over "tell the operator something" — kept
/// separate from the AWS SDK's own <c>IAmazonSimpleNotificationService</c> (a large surface this
/// codebase only ever needs one method of) so <see cref="StalenessDigestService"/> can be unit
/// tested against a trivial in-memory fake, same pattern <see cref="Data.Store.IHostedTable"/>
/// already established for DynamoDB.
/// </summary>
public interface IOperatorAlertPublisher
{
    Task PublishAsync(string subject, string message, CancellationToken cancellationToken = default);
}

public sealed class SnsOperatorAlertPublisher : IOperatorAlertPublisher
{
    private readonly Amazon.SimpleNotificationService.IAmazonSimpleNotificationService _sns;
    private readonly string? _topicArn;
    private readonly ILogger<SnsOperatorAlertPublisher> _logger;

    public SnsOperatorAlertPublisher(
        Amazon.SimpleNotificationService.IAmazonSimpleNotificationService sns,
        IConfiguration configuration,
        ILogger<SnsOperatorAlertPublisher> logger)
    {
        _sns = sns;
        _topicArn = configuration["Alerting:OperatorTopicArn"];
        _logger = logger;
    }

    public async Task PublishAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_topicArn))
        {
            // Never crash the caller over missing alerting config — log loudly (this itself would
            // need a human to notice, same class of gap this whole change exists to close, but
            // there's no second channel to escalate a misconfigured first one to) and return,
            // matching this project's existing pattern of graceful capability gating rather than a
            // hard failure on missing optional configuration.
            _logger.LogWarning("operator_alert_skipped_no_topic_arn subject={Subject}", subject);
            return;
        }

        await _sns.PublishAsync(new Amazon.SimpleNotificationService.Model.PublishRequest
        {
            TopicArn = _topicArn,
            Subject = subject,
            Message = message,
        }, cancellationToken);
    }
}

/// <summary>
/// operator-alerting design.md: re-runs <see cref="UploadStalenessCalculator"/> — the exact same
/// judgment <see cref="DashboardService"/> already computes per-project on page load — across every
/// project in every organization, once a day, and emails the operator a single digest of whatever is
/// currently stale. No dedup/"already notified" state (design.md's Decisions): a project stale for a
/// week appears in the digest every day it's stale, deliberately.
/// </summary>
public sealed class StalenessDigestService
{
    private readonly IProjectRepository _projects;
    private readonly IOrganizationRepository _organizations;
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IOperatorAlertPublisher _alerts;
    private readonly ILogger<StalenessDigestService> _logger;

    public StalenessDigestService(
        IProjectRepository projects,
        IOrganizationRepository organizations,
        ICaseReportRepository caseReports,
        IFlagProofReportRepository flagProofReports,
        IOperatorAlertPublisher alerts,
        ILogger<StalenessDigestService> logger)
    {
        _projects = projects;
        _organizations = organizations;
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var allProjects = await _projects.ListAllAsync(cancellationToken);
        var staleLines = new List<string>();

        foreach (var project in allProjects)
        {
            var caseReports = await _caseReports.ListByProjectAsync(project.Id, cancellationToken);
            var flagProofReports = await _flagProofReports.ListByProjectAsync(project.Id, cancellationToken);
            var uploadTimestamps = caseReports.Select(r => r.UploadedAt)
                .Concat(flagProofReports.Select(r => r.UploadedAt))
                .ToList();

            if (!UploadStalenessCalculator.IsStale(uploadTimestamps, DateTimeOffset.UtcNow))
            {
                continue;
            }

            var org = await _organizations.GetAsync(project.OrganizationId, cancellationToken);
            var lastUpload = uploadTimestamps.Count > 0 ? uploadTimestamps.Max().ToString("u") : "never";
            staleLines.Add($"- {org?.Name ?? project.OrganizationId.ToString()} / {project.Name} (last upload: {lastUpload})");
        }

        if (staleLines.Count == 0)
        {
            _logger.LogInformation("staleness_digest_run stale_count=0");
            return;
        }

        _logger.LogInformation("staleness_digest_run stale_count={StaleCount}", staleLines.Count);

        var message = $"{staleLines.Count} project(s) currently stale (no upload within 3x their typical cadence):\n\n"
            + string.Join("\n", staleLines);

        await _alerts.PublishAsync($"ReleaseTwin: {staleLines.Count} project(s) gone quiet", message, cancellationToken);
    }
}
