namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// upload-staleness spec: judges a project stale by comparing the time since its most recent
/// upload against the median gap between its own past uploads (design.md: median, not mean, so a
/// burst of near-simultaneous uploads doesn't drag the "typical" gap down enough to cause false
/// positives on an otherwise steady cadence). Pure function over timestamps — no repository or DB
/// access of its own, so it can be called inline from DashboardService.
/// </summary>
public static class UploadStalenessCalculator
{
    private const int MinimumUploadsToJudge = 5;
    private const int ThresholdMultiplier = 3;

    public static bool IsStale(IReadOnlyList<DateTimeOffset> uploadTimestamps, DateTimeOffset now)
    {
        if (uploadTimestamps.Count < MinimumUploadsToJudge)
        {
            return false;
        }

        var sorted = uploadTimestamps.OrderBy(t => t).ToList();
        var gaps = new List<TimeSpan>();
        for (var i = 1; i < sorted.Count; i++)
        {
            gaps.Add(sorted[i] - sorted[i - 1]);
        }

        var typicalGap = Median(gaps);
        var currentGap = now - sorted[^1];
        return currentGap > typicalGap * ThresholdMultiplier;
    }

    private static TimeSpan Median(List<TimeSpan> gaps)
    {
        var sorted = gaps.OrderBy(g => g).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
