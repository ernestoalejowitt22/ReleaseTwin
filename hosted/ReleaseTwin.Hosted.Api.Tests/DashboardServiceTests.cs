using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// hosted-react-frontend: DashboardService is the extracted, directly-testable data-shaping logic
/// behind the /api/dashboard endpoint — same scenarios DashboardModelTests exercised against the
/// old Razor Page, now against the service the JSON endpoint actually calls.
/// </summary>
public class DashboardServiceTests
{
    private sealed record Fixture(ProvisioningService Provisioning, DashboardService Dashboard, IConnectionRepository Connections, ICaseReportRepository CaseReports);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var users = new UserRepository(table);
        var projects = new ProjectRepository(table);
        var tokens = new ApiTokenRepository(table);
        var connections = new ConnectionRepository(table);
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);
        var usage = new UsageCounterRepository(table);

        var provisioning = new ProvisioningService(users, projects, tokens, new TokenService());
        var dashboard = new DashboardService(projects, connections, tokens, caseReports, flagProofReports, usage);
        return new Fixture(provisioning, dashboard, connections, caseReports);
    }

    // Scenario: Cross-organization data is never shown
    [Fact]
    public async Task CustomerSeesOnlyTheirOwnOrganizationsProjects()
    {
        var f = NewFixture();
        var orgAUser = await f.Provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await f.Provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectA = await f.Provisioning.CreateProjectAsync(orgAUser.OrganizationId, "A's project");
        await f.Provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");

        var view = await f.Dashboard.GetDashboardViewAsync(orgAUser.OrganizationId, null);

        Assert.Single(view.Projects);
        Assert.Equal(projectA.Id, view.Projects[0].Id);
    }

    [Fact]
    public async Task RequestingAnotherOrgsProjectDoesNotSelectIt()
    {
        var f = NewFixture();
        var orgAUser = await f.Provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await f.Provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectB = await f.Provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");

        var view = await f.Dashboard.GetDashboardViewAsync(orgAUser.OrganizationId, projectB.Id);

        Assert.Null(view.SelectedProject);
    }

    // Scenario: Uploaded reports appear in run history
    [Fact]
    public async Task UploadedReportsAppearInRunHistory()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await f.Provisioning.CreateProjectAsync(user.OrganizationId, "P");
        await f.CaseReports.AddAsync(new Data.Entities.UploadedCaseReport
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CaseId = "CASE-1",
            OracleLocator = "tickets/CASE-1",
            FixtureSha256 = "abc",
            Passed = true,
            CleanupStatus = "AllSucceeded",
            UploadedAt = DateTimeOffset.UtcNow,
        });

        var view = await f.Dashboard.GetDashboardViewAsync(user.OrganizationId, project.Id);

        Assert.Single(view.CaseReports);
        Assert.Equal("CASE-1", view.CaseReports[0].CaseId);
    }

    // usage-metering: Dashboard shows the organization's current usage
    [Fact]
    public async Task UsageSummaryIsZeroWhenNothingUploaded()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);

        var view = await f.Dashboard.GetDashboardViewAsync(user.OrganizationId, null);

        Assert.Equal(0, view.Usage.CaseReportCount);
        Assert.Equal(0, view.Usage.FlagProofReportCount);
    }
}
