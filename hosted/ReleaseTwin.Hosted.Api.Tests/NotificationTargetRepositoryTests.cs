using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class NotificationTargetRepositoryTests
{
    [Fact]
    public async Task RoundTripsListsAndRecordsOutcome()
    {
        var table = new InMemoryHostedTable();
        var repo = new NotificationTargetRepository(table);
        var projectId = Guid.NewGuid();

        var a = new NotificationTarget { Id = Guid.NewGuid(), ProjectId = projectId, Kind = NotificationTargetKind.Slack, Url = "https://a", Enabled = true, CreatedAt = DateTimeOffset.UtcNow };
        var b = new NotificationTarget { Id = Guid.NewGuid(), ProjectId = projectId, Kind = NotificationTargetKind.Webhook, Url = "https://b", Enabled = false, CreatedAt = DateTimeOffset.UtcNow };
        await repo.PutAsync(a);
        await repo.PutAsync(b);

        var list = await repo.ListByProjectAsync(projectId);
        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(list, t => t.ProjectId != projectId);

        await repo.RecordOutcomeAsync(projectId, a.Id, "success", DateTimeOffset.UtcNow);
        var reloaded = await repo.GetAsync(projectId, a.Id);
        Assert.Equal("success", reloaded!.LastOutcome);
        Assert.NotNull(reloaded.LastAttemptAt);

        await repo.DeleteAsync(projectId, a.Id);
        Assert.Null(await repo.GetAsync(projectId, a.Id));
        Assert.Single(await repo.ListByProjectAsync(projectId));

        // Recording an outcome for a deleted target is a no-op, not a throw.
        await repo.RecordOutcomeAsync(projectId, a.Id, "success", DateTimeOffset.UtcNow);
    }
}

public class IngestNotificationEnqueueTests
{
    private sealed class CapturingQueue : INotificationQueue
    {
        public List<RunNotification> Enqueued { get; } = [];
        public Task EnqueueAsync(RunNotification notification, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static async Task<(HttpClient Client, Guid ProjectId)> ClientWithTokenAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Proj");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        return (client, project.Id);
    }

    [Fact]
    public async Task FailedCaseReportEnqueuesWhenFlagOn()
    {
        var queue = new CapturingQueue();
        using var factory = new CustomWebApplicationFactory
        {
            NotificationQueueForTesting = queue,
            ExtraConfiguration = new Dictionary<string, string?> { ["FeatureFlags:run-notifications"] = "true" },
        };
        var (client, _) = await ClientWithTokenAsync(factory);

        await client.PostAsJsonAsync("/api/ingest/case-report", new IngestCaseReportRequest
        {
            CaseId = "CLM-9", OracleLocator = "t/CLM-9", FixtureSha256 = "x",
            Passed = false, Classification = "Infrastructure", CleanupStatus = "AllSucceeded", DurationMs = 1,
        });

        var n = Assert.Single(queue.Enqueued);
        Assert.Equal("CLM-9", n.CaseId);
        Assert.Equal("failed", n.Result);
        Assert.Equal("case", n.ReportKind);
    }

    [Fact]
    public async Task PassingReportDoesNotEnqueue()
    {
        var queue = new CapturingQueue();
        using var factory = new CustomWebApplicationFactory
        {
            NotificationQueueForTesting = queue,
            ExtraConfiguration = new Dictionary<string, string?> { ["FeatureFlags:run-notifications"] = "true" },
        };
        var (client, _) = await ClientWithTokenAsync(factory);

        await client.PostAsJsonAsync("/api/ingest/case-report", new IngestCaseReportRequest
        {
            CaseId = "OK-1", OracleLocator = "t/OK-1", FixtureSha256 = "x",
            Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 1,
        });

        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task FailedReportDoesNotEnqueueWhenFlagOff()
    {
        var queue = new CapturingQueue();
        using var factory = new CustomWebApplicationFactory { NotificationQueueForTesting = queue }; // flag default false
        var (client, _) = await ClientWithTokenAsync(factory);

        await client.PostAsJsonAsync("/api/ingest/case-report", new IngestCaseReportRequest
        {
            CaseId = "CLM-9", OracleLocator = "t/CLM-9", FixtureSha256 = "x",
            Passed = false, CleanupStatus = "AllSucceeded", DurationMs = 1,
        });

        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task IneligibleFlagProofEnqueues()
    {
        var queue = new CapturingQueue();
        using var factory = new CustomWebApplicationFactory
        {
            NotificationQueueForTesting = queue,
            ExtraConfiguration = new Dictionary<string, string?> { ["FeatureFlags:run-notifications"] = "true" },
        };
        var (client, _) = await ClientWithTokenAsync(factory);

        await client.PostAsJsonAsync("/api/ingest/flag-proof-report", new IngestFlagProofReportRequest
        {
            CaseId = "FP-1", OracleLocator = "t/FP-1", BuildIdentity = "build-42", Outcome = "Ineligible",
        });

        var n = Assert.Single(queue.Enqueued);
        Assert.Equal("ineligible", n.Result);
        Assert.Equal("flag-proof", n.ReportKind);
    }
}
