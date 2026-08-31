using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Releases;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>release-readiness-rollup: latest-wins, headline state, stale window, releases listing.</summary>
public class ReleaseRollupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(ReleaseRollupService Service, CaseReportRepository Cases, FlagProofReportRepository FlagProofs);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var cases = new CaseReportRepository(table);
        var flagProofs = new FlagProofReportRepository(table);
        return new Fixture(new ReleaseRollupService(cases, flagProofs), cases, flagProofs);
    }

    private static UploadedCaseReport Case(Guid projectId, string caseId, string? release, bool passed, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        OracleLocator = "o",
        FixtureSha256 = "s",
        Passed = passed,
        CleanupStatus = "Ok",
        DurationMs = 1,
        Release = release,
        UploadedAt = at,
        ProjectId = projectId,
    };

    private static UploadedFlagProofReport FlagProof(Guid projectId, string caseId, string? release, string outcome, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        OracleLocator = "o",
        BuildIdentity = "b",
        Outcome = outcome,
        Release = release,
        UploadedAt = at,
        ProjectId = projectId,
    };

    [Fact]
    public async Task ListReleasesReturnsDistinctLabelsAndIsEmptyWhenNoneAreLabelled()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        Assert.Empty(await f.Service.ListReleasesAsync(p));

        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now));
        await f.Cases.AddAsync(Case(p, "c2", "4.1", false, Now));
        await f.FlagProofs.AddAsync(FlagProof(p, "c3", "4.2", "Passed", Now));
        await f.Cases.AddAsync(Case(p, "c4", null, true, Now));

        var releases = await f.Service.ListReleasesAsync(p);
        Assert.Equal(new[] { "4.1", "4.2" }, releases);
    }

    [Fact]
    public async Task AllGreenYieldsProven()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-1)));
        await f.FlagProofs.AddAsync(FlagProof(p, "c2", "4.2", "Passed", Now.AddDays(-2)));

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseHeadlineState.Proven, rollup.Headline);
        Assert.Equal(2, rollup.GreenCount);
        Assert.Equal(0, rollup.FailingCount);
        Assert.Equal(0, rollup.StaleCount);
    }

    [Fact]
    public async Task OneFailingCaseYieldsNotProven()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-1)));
        await f.Cases.AddAsync(Case(p, "c2", "4.2", false, Now.AddDays(-1)));

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseHeadlineState.NotProven, rollup.Headline);
        Assert.Equal(1, rollup.FailingCount);
    }

    [Fact]
    public async Task AStaleCaseWithNoFailuresYieldsIncomplete()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-1)));
        await f.Cases.AddAsync(Case(p, "c2", "4.2", true, Now.AddDays(-40))); // older than the 14d window

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseHeadlineState.Incomplete, rollup.Headline);
        Assert.Equal(1, rollup.StaleCount);
        Assert.Equal(ReleaseCaseState.Stale, rollup.Cases.Single(c => c.CaseId == "c2").State);
    }

    [Fact]
    public async Task LatestResultWins()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", false, Now.AddDays(-3)));
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-1)));

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseCaseState.Green, rollup.Cases.Single().State);
        Assert.Equal(ReleaseHeadlineState.Proven, rollup.Headline);
    }

    [Fact]
    public async Task AnIneligibleFlagProofCountsAsStale()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-2)));
        await f.FlagProofs.AddAsync(FlagProof(p, "c1", "4.2", "Ineligible", Now.AddDays(-1)));

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseCaseState.Stale, rollup.Cases.Single().State);
        Assert.Equal(ReleaseHeadlineState.Incomplete, rollup.Headline);
    }

    [Fact]
    public async Task ReleaseWithOnlyStaleRunsIsIncomplete()
    {
        var f = NewFixture();
        var p = Guid.NewGuid();
        await f.Cases.AddAsync(Case(p, "c1", "4.2", true, Now.AddDays(-90)));

        var rollup = await f.Service.RollupAsync(p, "4.2", 14, Now);

        Assert.Equal(ReleaseHeadlineState.Incomplete, rollup.Headline);
    }
}
