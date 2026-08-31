namespace ReleaseTwin.Hosted.Api.Analytics;

/// <summary>trend-analytics: the selectable trend window. Fixed set — see design.md D1 (fixed bucket granularity, not a free <c>bucket</c> param).</summary>
public enum TrendWindow
{
    SevenDays,
    ThirtyDays,
    NinetyDays,
}

public static class TrendWindowParsing
{
    /// <summary>Parses the <c>window=</c> query value. Only <c>7d</c>/<c>30d</c>/<c>90d</c> are valid; anything else (including null) returns false.</summary>
    public static bool TryParse(string? value, out TrendWindow window)
    {
        switch (value)
        {
            case "7d": window = TrendWindow.SevenDays; return true;
            case "30d": window = TrendWindow.ThirtyDays; return true;
            case "90d": window = TrendWindow.NinetyDays; return true;
            default: window = TrendWindow.ThirtyDays; return false;
        }
    }

    public static int Days(this TrendWindow window) => window switch
    {
        TrendWindow.SevenDays => 7,
        TrendWindow.ThirtyDays => 30,
        TrendWindow.NinetyDays => 90,
        _ => 30,
    };

    /// <summary>90-day windows bucket by ISO week (Monday start, UTC); shorter windows bucket by UTC day.</summary>
    public static bool IsWeekly(this TrendWindow window) => window == TrendWindow.NinetyDays;
}

/// <summary>
/// trend-analytics: one time bucket. Rates are <c>null</c> — rendered as a gap — when their
/// denominator is zero, never <c>0</c>, which would misread as "everything failed".
/// </summary>
public sealed record TrendBucket(
    DateTimeOffset Start,
    double? CasePassRate,
    double? FlagProofPassRate,
    int RunVolume,
    IReadOnlyDictionary<string, int> ClassificationBreakdown);

/// <summary>trend-analytics: a case whose pass/fail outcome flipped within the window, with its flip count. Cases that never flipped are omitted.</summary>
public sealed record FlakiestCase(string CaseId, int FlipCount, DateTimeOffset LastActivity);

public sealed record TrendReport(
    TrendWindow Window,
    string Granularity,
    IReadOnlyList<TrendBucket> Buckets,
    IReadOnlyList<FlakiestCase> FlakiestCases);
