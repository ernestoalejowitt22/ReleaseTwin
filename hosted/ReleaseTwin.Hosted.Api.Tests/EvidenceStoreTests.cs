using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class EvidenceStoreTests
{
    private static (RunEvidenceRepository Repo, ProjectRepository Projects, InMemoryEvidenceBlobStore Blobs) NewStore()
    {
        var table = new InMemoryHostedTable();
        return (new RunEvidenceRepository(table), new ProjectRepository(table), new InMemoryEvidenceBlobStore());
    }

    private static UploadedRunEvidence Doc(Guid projectId, Guid reportId, DateTimeOffset uploadedAt, params string[] screenshotIds) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        ReportId = reportId,
        ReportKind = "case",
        DocumentJson = "{\"legs\":[]}",
        ScreenshotIds = screenshotIds,
        UploadedAt = uploadedAt,
    };

    [Fact]
    public async Task EvidenceLinksToExactlyOneReport()
    {
        var (repo, _, _) = NewStore();
        var projectId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        await repo.AddAsync(Doc(projectId, reportId, DateTimeOffset.UtcNow));

        Assert.NotNull(await repo.GetByReportAsync(projectId, reportId));
        Assert.Null(await repo.GetByReportAsync(projectId, Guid.NewGuid()));
        Assert.Null(await repo.GetByReportAsync(Guid.NewGuid(), reportId));
    }

    [Fact]
    public async Task Purge_RemovesExpiredEvidence_AndDeletesBlobs_ButKeepsRecentEvidence()
    {
        var (repo, projects, blobs) = NewStore();
        var project = await projects.CreateAsync(Guid.NewGuid(), "P"); // default 30-day retention

        var oldReport = Guid.NewGuid();
        var freshReport = Guid.NewGuid();
        await blobs.PutAsync(project.Id, "shot-1", new byte[] { 1 });
        await repo.AddAsync(Doc(project.Id, oldReport, DateTimeOffset.UtcNow.AddDays(-40), "shot-1"));
        await repo.AddAsync(Doc(project.Id, freshReport, DateTimeOffset.UtcNow.AddDays(-1)));

        var purge = new EvidencePurgeService(repo, blobs, projects, NullLogger<EvidencePurgeService>.Instance);
        var purged = await purge.RunAsync();

        Assert.Equal(1, purged);
        Assert.Null(await repo.GetByReportAsync(project.Id, oldReport));
        Assert.NotNull(await repo.GetByReportAsync(project.Id, freshReport));
        Assert.Null(await blobs.GetAsync(project.Id, "shot-1"));
    }

    [Fact]
    public async Task Purge_HonorsLoweredRetentionWindowImmediately()
    {
        var (repo, projects, blobs) = NewStore();
        var project = await projects.CreateAsync(Guid.NewGuid(), "P");
        var reportId = Guid.NewGuid();
        await repo.AddAsync(Doc(project.Id, reportId, DateTimeOffset.UtcNow.AddDays(-10)));

        var purge = new EvidencePurgeService(repo, blobs, projects, NullLogger<EvidencePurgeService>.Instance);
        Assert.Equal(0, await purge.RunAsync()); // still within default 30 days

        await projects.SetEvidenceConfigAsync(project.OrganizationId, project.Id, captureDefault: false, retentionDays: 7);
        Assert.Equal(1, await purge.RunAsync()); // now 10 days old > 7-day window
    }

    [Fact]
    public async Task IngestService_RejectsOversizeDocument()
    {
        var (repo, _, _) = NewStore();
        var service = new EvidenceIngestService(repo, new InMemoryEvidenceBlobStore(), new OrganizationRepository(new InMemoryHostedTable()), TestEntitlements.Service);
        var big = System.Text.Json.JsonSerializer.SerializeToElement(new { blob = new string('x', EvidenceIngestService.MaxDocumentBytes + 10) });

        Assert.False(service.IsWithinLimits(big, Array.Empty<UploadedScreenshot>(), out var reason));
        Assert.Contains("exceeds", reason);
    }

    [Fact]
    public async Task IngestService_FreeTierDropsEvidence_PaidTierStoresIt()
    {
        var table = new InMemoryHostedTable();
        var orgs = new OrganizationRepository(table);
        var users = new UserRepository(table);
        var projects = new ProjectRepository(table);
        var provisioning = new ProvisioningService(users, orgs, projects, new ApiTokenRepository(table), new TokenService(), TestEntitlements.Service);
        var repo = new RunEvidenceRepository(table);
        var service = new EvidenceIngestService(repo, new InMemoryEvidenceBlobStore(), orgs, TestEntitlements.Service);

        var user = await provisioning.GetOrCreateUserAsync("clerk-x", "x", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var doc = System.Text.Json.JsonSerializer.SerializeToElement(new { legs = Array.Empty<object>() });
        var reportId = Guid.NewGuid();

        var freeAccepted = await service.StoreAsync(user.OrganizationId, project.Id, reportId, "case", doc, Array.Empty<UploadedScreenshot>(), default);
        Assert.False(freeAccepted);
        Assert.Null(await repo.GetByReportAsync(project.Id, reportId));

        await provisioning.UpgradeToTeamAsync(user.OrganizationId);
        var paidAccepted = await service.StoreAsync(user.OrganizationId, project.Id, reportId, "case", doc, Array.Empty<UploadedScreenshot>(), default);
        Assert.True(paidAccepted);
        Assert.NotNull(await repo.GetByReportAsync(project.Id, reportId));
    }
}
