using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>trend-analytics: the windowed project-partition range read — half-open [from, to) semantics, empty range.</summary>
public class CaseReportRangeReadTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private static UploadedCaseReport Case(Guid projectId, DateTimeOffset uploadedAt) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = "c",
        OracleLocator = "o",
        FixtureSha256 = "s",
        Passed = true,
        CleanupStatus = "Ok",
        DurationMs = 1,
        UploadedAt = uploadedAt,
        ProjectId = projectId,
    };

    [Fact]
    public async Task RangeIsLowerInclusiveUpperExclusive()
    {
        var table = new InMemoryHostedTable();
        var repo = new CaseReportRepository(table);
        var project = Guid.NewGuid();

        await repo.AddAsync(Case(project, Anchor));                 // exactly at 'from' -> included
        await repo.AddAsync(Case(project, Anchor.AddDays(1)));      // inside -> included
        await repo.AddAsync(Case(project, Anchor.AddDays(2)));      // exactly at 'to' -> excluded
        await repo.AddAsync(Case(project, Anchor.AddDays(-1)));     // before -> excluded

        var got = await repo.ListByProjectInRangeAsync(project, Anchor, Anchor.AddDays(2));

        Assert.Equal(2, got.Count);
        Assert.All(got, r => Assert.InRange(r.UploadedAt, Anchor, Anchor.AddDays(2).AddTicks(-1)));
    }

    [Fact]
    public async Task EmptyRangeReturnsNothing()
    {
        var table = new InMemoryHostedTable();
        var repo = new FlagProofReportRepository(table);
        var project = Guid.NewGuid();
        await repo.AddAsync(new UploadedFlagProofReport
        {
            Id = Guid.NewGuid(), CaseId = "c", OracleLocator = "o", BuildIdentity = "b",
            Outcome = "Passed", UploadedAt = Anchor, ProjectId = project,
        });

        var got = await repo.ListByProjectInRangeAsync(project, Anchor.AddDays(5), Anchor.AddDays(10));

        Assert.Empty(got);
    }
}
