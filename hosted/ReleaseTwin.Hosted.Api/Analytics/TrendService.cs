using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Analytics;

/// <summary>
/// trend-analytics: time-bucketed pass-rate / flag-proof-rate / volume / classification series plus
/// a flakiest-cases list, computed by a windowed range query over the existing report items and an
/// in-memory aggregation — no precomputed rollups, no new write path (design.md). The organization
/// rollup fans out over the org's projects and merges buckets in memory (an org has a handful of
/// projects); no GSI (design.md D4).
/// </summary>
public sealed class TrendService
{
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IProjectRepository _projects;

    public TrendService(ICaseReportRepository caseReports, IFlagProofReportRepository flagProofReports, IProjectRepository projects)
    {
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _projects = projects;
    }

    public async Task<TrendReport> ForProjectAsync(Guid projectId, TrendWindow window, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var span = TrendSpan.For(window, now);
        var cases = await _caseReports.ListByProjectInRangeAsync(projectId, span.From, span.To, cancellationToken);
        var flagProofs = await _flagProofReports.ListByProjectInRangeAsync(projectId, span.From, span.To, cancellationToken);
        return Aggregate(window, span, cases, flagProofs);
    }

    public async Task<TrendReport> ForOrganizationAsync(Guid organizationId, TrendWindow window, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var span = TrendSpan.For(window, now);
        var projects = await _projects.ListByOrganizationAsync(organizationId, cancellationToken);

        var cases = new List<UploadedCaseReport>();
        var flagProofs = new List<UploadedFlagProofReport>();
        foreach (var project in projects)
        {
            cases.AddRange(await _caseReports.ListByProjectInRangeAsync(project.Id, span.From, span.To, cancellationToken));
            flagProofs.AddRange(await _flagProofReports.ListByProjectInRangeAsync(project.Id, span.From, span.To, cancellationToken));
        }

        return Aggregate(window, span, cases, flagProofs);
    }

    private static TrendReport Aggregate(
        TrendWindow window,
        TrendSpan span,
        IReadOnlyList<UploadedCaseReport> cases,
        IReadOnlyList<UploadedFlagProofReport> flagProofs)
    {
        var count = span.BucketCount;
        var caseBuckets = new List<UploadedCaseReport>[count];
        var flagProofBuckets = new List<UploadedFlagProofReport>[count];
        for (var i = 0; i < count; i++)
        {
            caseBuckets[i] = [];
            flagProofBuckets[i] = [];
        }

        foreach (var report in cases)
        {
            var index = span.BucketIndex(report.UploadedAt);
            if (index is >= 0 && index < count)
            {
                caseBuckets[index].Add(report);
            }
        }

        foreach (var report in flagProofs)
        {
            var index = span.BucketIndex(report.UploadedAt);
            if (index is >= 0 && index < count)
            {
                flagProofBuckets[index].Add(report);
            }
        }

        var buckets = new List<TrendBucket>(count);
        for (var i = 0; i < count; i++)
        {
            var bucketCases = caseBuckets[i];
            var bucketFlagProofs = flagProofBuckets[i];

            double? casePassRate = bucketCases.Count == 0
                ? null
                : (double)bucketCases.Count(r => r.Passed) / bucketCases.Count;

            var eligible = bucketFlagProofs.Count(r => !string.Equals(r.Outcome, "Ineligible", StringComparison.OrdinalIgnoreCase));
            double? flagProofPassRate = eligible == 0
                ? null
                : (double)bucketFlagProofs.Count(r => string.Equals(r.Outcome, "Passed", StringComparison.OrdinalIgnoreCase)) / eligible;

            var classification = bucketCases
                .Where(r => !r.Passed && !string.IsNullOrWhiteSpace(r.Classification))
                .GroupBy(r => r.Classification!)
                .ToDictionary(g => g.Key, g => g.Count());

            buckets.Add(new TrendBucket(
                new DateTimeOffset(span.BucketStart(i), TimeSpan.Zero),
                casePassRate,
                flagProofPassRate,
                bucketCases.Count + bucketFlagProofs.Count,
                classification));
        }

        return new TrendReport(window, span.Granularity, buckets, Flakiest(cases));
    }

    private static IReadOnlyList<FlakiestCase> Flakiest(IReadOnlyList<UploadedCaseReport> cases)
    {
        var flakiest = new List<FlakiestCase>();
        foreach (var group in cases.GroupBy(r => r.CaseId))
        {
            var ordered = group.OrderBy(r => r.UploadedAt).ToList();
            var flips = 0;
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Passed != ordered[i - 1].Passed)
                {
                    flips++;
                }
            }

            if (flips > 0)
            {
                flakiest.Add(new FlakiestCase(group.Key, flips, ordered[^1].UploadedAt));
            }
        }

        return flakiest
            .OrderByDescending(f => f.FlipCount)
            .ThenByDescending(f => f.LastActivity)
            .Take(5)
            .ToList();
    }

    /// <summary>The bucket geometry for a window: aligned start, bucket size, and count.</summary>
    private readonly record struct TrendSpan(DateTime FirstBucketStart, int BucketSizeDays, int BucketCount, string Granularity)
    {
        public DateTimeOffset From => new(FirstBucketStart, TimeSpan.Zero);

        public DateTimeOffset To => new(FirstBucketStart.AddDays((long)BucketSizeDays * BucketCount), TimeSpan.Zero);

        public DateTime BucketStart(int index) => FirstBucketStart.AddDays((long)BucketSizeDays * index);

        public int BucketIndex(DateTimeOffset at)
        {
            var offsetDays = (at.UtcDateTime - FirstBucketStart).TotalDays;
            return (int)Math.Floor(offsetDays / BucketSizeDays);
        }

        public static TrendSpan For(TrendWindow window, DateTimeOffset now)
        {
            var today = now.UtcDateTime.Date;

            if (window.IsWeekly())
            {
                // ISO week, Monday start. 90 days ≈ 13 weekly buckets, the last being the current week.
                var thisWeekMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                const int weeks = 13;
                return new TrendSpan(thisWeekMonday.AddDays(-7 * (weeks - 1)), 7, weeks, "weekly");
            }

            var days = window.Days();
            return new TrendSpan(today.AddDays(-(days - 1)), 1, days, "daily");
        }
    }
}
