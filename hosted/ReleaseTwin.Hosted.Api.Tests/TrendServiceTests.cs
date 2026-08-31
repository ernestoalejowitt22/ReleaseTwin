using ReleaseTwin.Hosted.Api.Analytics;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>trend-analytics: bucketing boundaries, null rates, flip counting, empty windows, classification sums, org fan-out.</summary>
public class TrendServiceTests
{
    private sealed record Fixture(
        TrendService Trends,
        CaseReportRepository CaseReports,
        FlagProofReportRepository FlagProofReports,
        ProjectRepository Projects,
        Services.ProvisioningService Provisioning);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);
        var projects = new ProjectRepository(table);
        var provisioning = new Services.ProvisioningService(
            new UserRepository(table), new OrganizationRepository(table), projects,
            new ApiTokenRepository(table), new Services.TokenService(), TestEntitlements.Service);
        return new Fixture(
            new TrendService(caseReports, flagProofReports, projects),
            caseReports, flagProofReports, projects, provisioning);
    }

    private static UploadedCaseReport Case(Guid projectId, DateTimeOffset uploadedAt, bool passed, string caseId = "case-1", string? classification = null) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        OracleLocator = "oracle://x",
        FixtureSha256 = "sha",
        Passed = passed,
        Classification = classification,
        CleanupStatus = "Ok",
        DurationMs = 1,
        UploadedAt = uploadedAt,
        ProjectId = projectId,
    };

    private static UploadedFlagProofReport FlagProof(Guid projectId, DateTimeOffset uploadedAt, string outcome) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = "case-1",
        OracleLocator = "oracle://x",
        BuildIdentity = "build-1",
        Outcome = outcome,
        UploadedAt = uploadedAt,
        ProjectId = projectId,
    };

    private static readonly DateTimeOffset Now = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero); // a Sunday

    [Fact]
    public async Task DailyWindowHasOneBucketPerDayAndAssignsReportsByUtcDay()
    {
        var f = NewFixture();
        var project = Guid.NewGuid();
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-1), passed: true));
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-1), passed: false));
        await f.CaseReports.AddAsync(Case(project, Now, passed: true));

        var report = await f.Trends.ForProjectAsync(project, TrendWindow.SevenDays, Now);

        Assert.Equal("daily", report.Granularity);
        Assert.Equal(7, report.Buckets.Count);
        Assert.Equal(0.5, report.Buckets[^2].CasePassRate);
        Assert.Equal(1.0, report.Buckets[^1].CasePassRate);
        Assert.Equal(2, report.Buckets[^2].RunVolume);
    }

    [Fact]
    public async Task EmptyBucketsAreZeroVolumeWithNullRates()
    {
        var f = NewFixture();
        var report = await f.Trends.ForProjectAsync(Guid.NewGuid(), TrendWindow.ThirtyDays, Now);

        Assert.Equal(30, report.Buckets.Count);
        Assert.All(report.Buckets, b =>
        {
            Assert.Equal(0, b.RunVolume);
            Assert.Null(b.CasePassRate);
            Assert.Null(b.FlagProofPassRate);
        });
        Assert.Empty(report.FlakiestCases);
    }

    [Fact]
    public async Task FlagProofRateExcludesIneligibleFromTheDenominator()
    {
        var f = NewFixture();
        var project = Guid.NewGuid();
        await f.FlagProofReports.AddAsync(FlagProof(project, Now, "Passed"));
        await f.FlagProofReports.AddAsync(FlagProof(project, Now, "BothFailed"));
        await f.FlagProofReports.AddAsync(FlagProof(project, Now, "Ineligible"));

        var report = await f.Trends.ForProjectAsync(project, TrendWindow.SevenDays, Now);

        Assert.Equal(0.5, report.Buckets[^1].FlagProofPassRate);
        Assert.Equal(3, report.Buckets[^1].RunVolume);
    }

    [Fact]
    public async Task FlagProofRateIsNullWhenEveryRunWasIneligible()
    {
        var f = NewFixture();
        var project = Guid.NewGuid();
        await f.FlagProofReports.AddAsync(FlagProof(project, Now, "Ineligible"));

        var report = await f.Trends.ForProjectAsync(project, TrendWindow.SevenDays, Now);

        Assert.Null(report.Buckets[^1].FlagProofPassRate);
    }

    [Fact]
    public async Task ClassificationBreakdownCountsFailedCasesByClassification()
    {
        var f = NewFixture();
        var project = Guid.NewGuid();
        await f.CaseReports.AddAsync(Case(project, Now, passed: false, classification: "Product"));
        await f.CaseReports.AddAsync(Case(project, Now, passed: false, classification: "Product"));
        await f.CaseReports.AddAsync(Case(project, Now, passed: false, classification: "Infrastructure"));
        await f.CaseReports.AddAsync(Case(project, Now, passed: true));

        var bucket = (await f.Trends.ForProjectAsync(project, TrendWindow.SevenDays, Now)).Buckets[^1];

        Assert.Equal(2, bucket.ClassificationBreakdown["Product"]);
        Assert.Equal(1, bucket.ClassificationBreakdown["Infrastructure"]);
    }

    [Fact]
    public async Task NinetyDayWindowBucketsByWeek()
    {
        var f = NewFixture();
        var report = await f.Trends.ForProjectAsync(Guid.NewGuid(), TrendWindow.NinetyDays, Now);

        Assert.Equal("weekly", report.Granularity);
        Assert.Equal(13, report.Buckets.Count);
        // Monday starts.
        Assert.All(report.Buckets, b => Assert.Equal(DayOfWeek.Monday, b.Start.UtcDateTime.DayOfWeek));
    }

    [Fact]
    public async Task FlakiestListRanksAlternatingCasesAboveStableOnesAndOmitsStable()
    {
        var f = NewFixture();
        var project = Guid.NewGuid();
        // case-A: pass, fail, pass, fail => 3 flips
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-4), passed: true, caseId: "case-A"));
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-3), passed: false, caseId: "case-A"));
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-2), passed: true, caseId: "case-A"));
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-1), passed: false, caseId: "case-A"));
        // case-B: always passes => 0 flips
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-2), passed: true, caseId: "case-B"));
        await f.CaseReports.AddAsync(Case(project, Now.AddDays(-1), passed: true, caseId: "case-B"));

        var flakiest = (await f.Trends.ForProjectAsync(project, TrendWindow.SevenDays, Now)).FlakiestCases;

        Assert.Single(flakiest);
        Assert.Equal("case-A", flakiest[0].CaseId);
        Assert.Equal(3, flakiest[0].FlipCount);
    }

    [Fact]
    public async Task OrganizationRollupMergesEveryProjectAndExcludesOtherOrgs()
    {
        var f = NewFixture();
        var mineUser = await f.Provisioning.GetOrCreateUserAsync("clerk-mine", "mine", null);
        var otherUser = await f.Provisioning.GetOrCreateUserAsync("clerk-other", "other", null);
        var mine = mineUser.OrganizationId;

        var p1 = await f.Projects.CreateAsync(mine, "p1");
        var p2 = await f.Projects.CreateAsync(mine, "p2");
        var pOther = await f.Projects.CreateAsync(otherUser.OrganizationId, "other");

        await f.CaseReports.AddAsync(Case(p1.Id, Now, passed: true));
        await f.CaseReports.AddAsync(Case(p2.Id, Now, passed: false));
        await f.CaseReports.AddAsync(Case(pOther.Id, Now, passed: true));

        var report = await f.Trends.ForOrganizationAsync(mine, TrendWindow.SevenDays, Now);

        Assert.Equal(2, report.Buckets[^1].RunVolume);
        Assert.Equal(0.5, report.Buckets[^1].CasePassRate);
    }
}
