using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>Hand-rolled fake, same pattern as InMemoryHostedTable — records every publish rather than actually talking to SNS.</summary>
public sealed class InMemoryOperatorAlertPublisher : IOperatorAlertPublisher
{
    public List<(string Subject, string Message)> Published { get; } = [];

    public Task PublishAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        Published.Add((subject, message));
        return Task.CompletedTask;
    }
}

/// <summary>
/// operator-alerting: exercises the daily digest across every organization's projects — the one
/// path in this codebase that deliberately crosses organization boundaries (every other repository
/// call is scoped to a single org's own partition).
/// </summary>
public class StalenessDigestServiceTests
{
    private sealed record Fixture(
        ProvisioningService Provisioning,
        StalenessDigestService Digest,
        ICaseReportRepository CaseReports,
        InMemoryOperatorAlertPublisher Alerts);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var users = new UserRepository(table);
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var tokens = new ApiTokenRepository(table);
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);

        var provisioning = new ProvisioningService(users, organizations, projects, tokens, new TokenService(), TestEntitlements.Service);
        var alerts = new InMemoryOperatorAlertPublisher();
        var digest = new StalenessDigestService(
            projects,
            organizations,
            caseReports,
            flagProofReports,
            alerts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StalenessDigestService>.Instance);
        return new Fixture(provisioning, digest, caseReports, alerts);
    }

    private static async Task SeedCaseReportAsync(Fixture f, Guid projectId, DateTimeOffset uploadedAt) =>
        await f.CaseReports.AddAsync(new UploadedCaseReport
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CaseId = $"CASE-{Guid.NewGuid()}",
            OracleLocator = "tickets/CASE",
            FixtureSha256 = "abc",
            Passed = true,
            CleanupStatus = "AllSucceeded",
            UploadedAt = uploadedAt,
        });

    [Fact]
    public async Task NothingStalePublishesNoDigest()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await f.Provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await SeedCaseReportAsync(f, project.Id, now.AddDays(-4 + i));
        }

        await f.Digest.RunAsync();

        Assert.Empty(f.Alerts.Published);
    }

    [Fact]
    public async Task StaleProjectIsNamedInTheDigest()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await f.Provisioning.CreateProjectAsync(user.OrganizationId, "Quiet Project");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await SeedCaseReportAsync(f, project.Id, now.AddDays(-30 + i));
        }

        await f.Digest.RunAsync();

        var publish = Assert.Single(f.Alerts.Published);
        Assert.Contains("1", publish.Subject);
        Assert.Contains("Quiet Project", publish.Message);
    }

    // Scenario: this is the one job that legitimately crosses organization boundaries.
    [Fact]
    public async Task DigestCoversProjectsAcrossEveryOrganization()
    {
        var f = NewFixture();
        var userA = await f.Provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var userB = await f.Provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectA = await f.Provisioning.CreateProjectAsync(userA.OrganizationId, "A's project");
        var projectB = await f.Provisioning.CreateProjectAsync(userB.OrganizationId, "B's project");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await SeedCaseReportAsync(f, projectA.Id, now.AddDays(-30 + i));
            await SeedCaseReportAsync(f, projectB.Id, now.AddDays(-30 + i));
        }

        await f.Digest.RunAsync();

        var publish = Assert.Single(f.Alerts.Published);
        Assert.Contains("A's project", publish.Message);
        Assert.Contains("B's project", publish.Message);
        Assert.Contains("2", publish.Subject);
    }

    [Fact]
    public async Task TooNewProjectIsNeverIncluded()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await f.Provisioning.CreateProjectAsync(user.OrganizationId, "P");
        await SeedCaseReportAsync(f, project.Id, DateTimeOffset.UtcNow.AddDays(-365));

        await f.Digest.RunAsync();

        Assert.Empty(f.Alerts.Published);
    }
}
