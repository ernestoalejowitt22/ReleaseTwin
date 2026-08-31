using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Flags;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// run-notifications (design D6): drains one <see cref="RunNotification"/> — resolves the project's
/// enabled targets, re-checks the org entitlement and the master flag at send time, POSTs to each
/// target with a short timeout and no redirects, and records the per-target outcome. Per-target HTTP
/// failures are recorded, not thrown (so a retry never double-notifies a target that already
/// succeeded); a failure to even load the org/flag throws, so SQS retries the message and it lands
/// in the DLQ after the redrive limit.
/// </summary>
public sealed class NotificationDispatchService
{
    /// <summary>The name of the <see cref="HttpClient"/> configured with no auto-redirect and a short timeout.</summary>
    public const string HttpClientName = "notifications";

    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly INotificationTargetRepository _targets;
    private readonly IEntitlementService _entitlements;
    private readonly IFlagService _flags;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDispatchService> _logger;
    private readonly Func<string, IPAddress[]>? _resolveHost;

    public NotificationDispatchService(
        IOrganizationRepository organizations,
        IProjectRepository projects,
        INotificationTargetRepository targets,
        IEntitlementService entitlements,
        IFlagService flags,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotificationDispatchService> logger,
        Func<string, IPAddress[]>? resolveHost = null)
    {
        _organizations = organizations;
        _projects = projects;
        _targets = targets;
        _entitlements = entitlements;
        _flags = flags;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _resolveHost = resolveHost;
    }

    public async Task DispatchAsync(RunNotification n, CancellationToken cancellationToken = default)
    {
        if (!await _flags.GetBooleanAsync("run-notifications", cancellationToken: cancellationToken))
        {
            _logger.LogInformation("run_notification_dropped reason=flag-off project={ProjectId}", n.ProjectId);
            return;
        }

        var organization = await _organizations.GetAsync(n.OrganizationId, cancellationToken);
        if (organization is null || !_entitlements.For(organization).RunNotifications)
        {
            _logger.LogInformation("run_notification_dropped reason=not-entitled org={OrganizationId}", n.OrganizationId);
            return;
        }

        var targets = (await _targets.ListByProjectAsync(n.ProjectId, cancellationToken)).Where(t => t.Enabled).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var project = await _projects.GetAsync(n.OrganizationId, n.ProjectId, cancellationToken);
        var projectName = project?.Name ?? "your project";
        var dashboardUrl = DashboardUrl(n.ProjectId);
        var client = _httpClientFactory.CreateClient(HttpClientName);

        foreach (var target in targets)
        {
            var outcome = await DeliverAsync(client, target, n, projectName, dashboardUrl, cancellationToken);
            await _targets.RecordOutcomeAsync(n.ProjectId, target.Id, outcome, DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    private async Task<string> DeliverAsync(HttpClient client, NotificationTarget target, RunNotification n, string projectName, string dashboardUrl, CancellationToken cancellationToken)
    {
        // design D6: re-check the address at send time — DNS may have changed since the target was saved.
        if (!OutboundUrlValidator.IsAllowed(target.Url, out var reason, _resolveHost))
        {
            _logger.LogWarning("run_notification_target_blocked target={TargetId} reason={Reason}", target.Id, reason);
            return $"failed: {reason}";
        }

        object body = target.Kind == NotificationTargetKind.Slack
            ? new { text = SlackText(projectName, n, dashboardUrl) }
            : new
            {
                project = projectName,
                caseId = n.CaseId,
                result = n.Result,
                classification = n.Classification,
                reportId = n.ReportId,
                reportKind = n.ReportKind,
                url = dashboardUrl,
            };

        try
        {
            using var response = await client.PostAsJsonAsync(target.Url, body, cancellationToken);
            return response.IsSuccessStatusCode ? "success" : $"failed: HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "run_notification_delivery_failed target={TargetId}", target.Id);
            return $"failed: {ex.GetType().Name}";
        }
    }

    private static string SlackText(string projectName, RunNotification n, string dashboardUrl)
    {
        var classification = string.IsNullOrWhiteSpace(n.Classification) ? "" : $" ({n.Classification})";
        var what = n.ReportKind == "flag-proof" ? "flag proof" : "case";
        return $"ReleaseTwin — *{projectName}*: {what} `{n.CaseId}` {n.Result}{classification}\n{dashboardUrl}";
    }

    private string DashboardUrl(Guid projectId)
    {
        var baseUrl = _configuration["Web:BaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? $"/dashboard?projectId={projectId}"
            : $"{baseUrl}/dashboard?projectId={projectId}";
    }
}
