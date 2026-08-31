using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Releases;

/// <summary>
/// release-readiness-rollup: aggregates a project's case and flag-proof reports by their
/// case-declared <c>release</c> label into a per-release readiness view. Reads a project's reports
/// with the existing native partition query and groups in memory — no GSI, no precomputed rollup
/// (design.md D-A).
/// </summary>
public sealed class ReleaseRollupService
{
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;

    public ReleaseRollupService(ICaseReportRepository caseReports, IFlagProofReportRepository flagProofReports)
    {
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
    }

    public async Task<IReadOnlyList<string>> ListReleasesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var cases = await _caseReports.ListByProjectAsync(projectId, cancellationToken);
        var flagProofs = await _flagProofReports.ListByProjectAsync(projectId, cancellationToken);

        return cases.Select(r => r.Release)
            .Concat(flagProofs.Select(r => r.Release))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ReleaseRollup> RollupAsync(Guid projectId, string release, int windowDays, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var cutoff = now.AddDays(-windowDays);

        var cases = (await _caseReports.ListByProjectAsync(projectId, cancellationToken))
            .Where(r => string.Equals(r.Release, release, StringComparison.Ordinal));
        var flagProofs = (await _flagProofReports.ListByProjectAsync(projectId, cancellationToken))
            .Where(r => string.Equals(r.Release, release, StringComparison.Ordinal));

        // The single most recent report of either kind wins for each case (design.md D-B).
        var latestByCase = new Dictionary<string, (DateTimeOffset At, ReleaseCaseState State, string Outcome)>(StringComparer.Ordinal);

        void Consider(string caseId, DateTimeOffset at, ReleaseCaseState state, string outcome)
        {
            if (!latestByCase.TryGetValue(caseId, out var current) || at > current.At)
            {
                latestByCase[caseId] = (at, state, outcome);
            }
        }

        foreach (var report in cases)
        {
            Consider(report.CaseId, report.UploadedAt, report.Passed ? ReleaseCaseState.Green : ReleaseCaseState.Failing,
                report.Passed ? "passed" : "failed");
        }

        foreach (var report in flagProofs)
        {
            var state = report.Outcome switch
            {
                "Passed" => ReleaseCaseState.Green,
                "Ineligible" => ReleaseCaseState.Stale,
                _ => ReleaseCaseState.Failing,
            };
            Consider(report.CaseId, report.UploadedAt, state, report.Outcome);
        }

        var results = latestByCase
            .Select(kv =>
            {
                var (at, state, outcome) = kv.Value;
                // The recency check is applied last: an old "green" result is stale, because
                // readiness is a statement about now (design.md D-B).
                if (at < cutoff)
                {
                    state = ReleaseCaseState.Stale;
                }

                return new ReleaseCaseResult(kv.Key, state, outcome, at);
            })
            .OrderBy(r => r.CaseId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var green = results.Count(r => r.State == ReleaseCaseState.Green);
        var failing = results.Count(r => r.State == ReleaseCaseState.Failing);
        var stale = results.Count(r => r.State == ReleaseCaseState.Stale);

        var headline = failing > 0
            ? ReleaseHeadlineState.NotProven
            : (stale > 0 || results.Count == 0)
                ? ReleaseHeadlineState.Incomplete
                : ReleaseHeadlineState.Proven;

        return new ReleaseRollup(release, headline, green, failing, stale, windowDays, results);
    }
}
