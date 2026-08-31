namespace ReleaseTwin.Hosted.Api.Releases;

/// <summary>release-readiness-rollup: a single case's standing under a release, from its latest report (design.md D-B).</summary>
public enum ReleaseCaseState
{
    /// <summary>Latest result is a recent <c>passed</c> case report or <c>Passed</c> flag-proof.</summary>
    Green,

    /// <summary>Latest result is a recent <c>failed</c> case report or a discriminating-failure flag-proof outcome.</summary>
    Failing,

    /// <summary>Latest report is older than the recency window, or the latest flag-proof result is <c>Ineligible</c>.</summary>
    Stale,
}

/// <summary>release-readiness-rollup: the ship-gate headline for a release (design.md D-C: failing &gt; stale &gt; proven).</summary>
public enum ReleaseHeadlineState
{
    Proven,
    NotProven,
    Incomplete,
}

public sealed record ReleaseCaseResult(string CaseId, ReleaseCaseState State, string LatestOutcome, DateTimeOffset LatestReportAt);

public sealed record ReleaseRollup(
    string Release,
    ReleaseHeadlineState Headline,
    int GreenCount,
    int FailingCount,
    int StaleCount,
    int WindowDays,
    IReadOnlyList<ReleaseCaseResult> Cases);

public static class ReleaseWindowParsing
{
    /// <summary>Parses the recency <c>window=</c> value. Allowlist only; default 14 days (design.md D-D).</summary>
    public static bool TryParse(string? value, out int windowDays)
    {
        switch (value)
        {
            case "7d": windowDays = 7; return true;
            case "14d": windowDays = 14; return true;
            case "30d": windowDays = 30; return true;
            case "90d": windowDays = 90; return true;
            default: windowDays = 14; return false;
        }
    }
}
