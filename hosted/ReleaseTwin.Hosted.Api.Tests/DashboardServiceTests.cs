using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// hosted-react-frontend: DashboardService is the extracted, directly-testable data-shaping logic
/// behind the /api/dashboard endpoint — same scenarios DashboardModelTests exercised against the
/// old Razor Page, now against the service the JSON endpoint actually calls.
/// </summary>
public class DashboardServiceTests
{
    private static HostedDbContext NewDb() => new(
        new DbContextOptionsBuilder<HostedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Scenario: Cross-organization data is never shown
    [Fact]
    public async Task CustomerSeesOnlyTheirOwnOrganizationsProjects()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var orgAUser = await provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectA = await provisioning.CreateProjectAsync(orgAUser.OrganizationId, "A's project");
        await provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");

        var view = await new DashboardService(db).GetDashboardViewAsync(orgAUser.OrganizationId, null);

        Assert.Single(view.Projects);
        Assert.Equal(projectA.Id, view.Projects[0].Id);
    }

    [Fact]
    public async Task RequestingAnotherOrgsProjectDoesNotSelectIt()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var orgAUser = await provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectB = await provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");

        var view = await new DashboardService(db).GetDashboardViewAsync(orgAUser.OrganizationId, projectB.Id);

        Assert.Null(view.SelectedProject);
    }

    // Scenario: Uploaded reports appear in run history
    [Fact]
    public async Task UploadedReportsAppearInRunHistory()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var user = await provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        db.UploadedCaseReports.Add(new Data.Entities.UploadedCaseReport
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CaseId = "CASE-1",
            OracleLocator = "t/1",
            FixtureSha256 = "abc",
            Passed = true,
            CleanupStatus = "AllSucceeded",
            DurationMs = 5,
            UploadedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var view = await new DashboardService(db).GetDashboardViewAsync(user.OrganizationId, project.Id);

        Assert.Single(view.CaseReports);
        Assert.Equal("CASE-1", view.CaseReports[0].CaseId);
    }

    // Scenario: Flag-proof result is not shown as an ordinary pass/fail
    [Fact]
    public async Task FlagProofResultsAreExposedSeparatelyFromCaseReports()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var user = await provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        db.UploadedFlagProofReports.Add(new Data.Entities.UploadedFlagProofReport
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CaseId = "CLM-042",
            OracleLocator = "t/1",
            BuildIdentity = "build-1",
            Outcome = "WeakOracle",
            KnownBadLegPassed = true,
            KnownGoodLegPassed = true,
            UploadedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var view = await new DashboardService(db).GetDashboardViewAsync(user.OrganizationId, project.Id);

        Assert.Empty(view.CaseReports);
        Assert.Single(view.FlagProofReports);
        Assert.Equal("WeakOracle", view.FlagProofReports[0].Outcome);
    }

    // Scenario: Connected project shows its repo
    [Fact]
    public async Task ConnectedProjectIncludesItsConnection()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var user = await provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        await new ConnectionService(db).ConnectAsync(project.Id, "github", "acme/checkout-service");

        var view = await new DashboardService(db).GetDashboardViewAsync(user.OrganizationId, project.Id);

        Assert.NotNull(view.Connection);
        Assert.Equal("acme/checkout-service", view.Connection!.ExternalRepo);
    }
}
