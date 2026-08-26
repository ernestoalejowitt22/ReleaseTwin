using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class UploadStalenessCalculatorTests
{
    private static readonly DateTimeOffset Day0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Day(double offsetDays) => Day0.AddDays(offsetDays);

    // Scenario: Too little history to judge
    [Fact]
    public void FewerThanFiveUploadsIsNeverStale()
    {
        var timestamps = new[] { Day(0), Day(1), Day(2), Day(3) };

        Assert.False(UploadStalenessCalculator.IsStale(timestamps, Day(1000)));
    }

    // Scenario: Upload gap within normal cadence / Upload gap exceeds normal cadence
    [Fact]
    public void GapAtOrBelowThreeTimesMedianIsNotStale()
    {
        // 5 uploads, 1-day gaps -> median gap = 1 day. Boundary: exactly 3 days since last upload.
        var timestamps = new[] { Day(0), Day(1), Day(2), Day(3), Day(4) };

        Assert.False(UploadStalenessCalculator.IsStale(timestamps, Day(4 + 3)));
    }

    [Fact]
    public void GapAboveThreeTimesMedianIsStale()
    {
        var timestamps = new[] { Day(0), Day(1), Day(2), Day(3), Day(4) };

        Assert.True(UploadStalenessCalculator.IsStale(timestamps, Day(4 + 3).AddSeconds(1)));
    }

    // Scenario: median resists a burst of near-simultaneous uploads that would drag a mean down
    [Fact]
    public void BurstOfNearSimultaneousUploadsDoesNotCauseFalseStale()
    {
        // Mostly-daily cadence (six 1-day gaps) plus one small burst pair right after the last
        // daily upload (two near-zero gaps). A mean gap would be dragged well below 1 day by the
        // burst; the median stays at 1 day since only 2 of 8 gaps are near-zero.
        var timestamps = new[]
        {
            Day(0), Day(1), Day(2), Day(3), Day(4), Day(5), Day(6),
            Day(6).AddSeconds(1), Day(6).AddSeconds(2),
        };

        // 2.5 days since the last upload: within 3x the true (median) daily cadence, but would
        // exceed 3x a mean-based cadence dragged down by the burst.
        Assert.False(UploadStalenessCalculator.IsStale(timestamps, Day(6).AddSeconds(2).AddDays(2.5)));
    }

    // Scenario: Infrequent but steady cadence is not penalized
    [Fact]
    public void InfrequentButSteadyCadenceToleratesProportionalGap()
    {
        // 5 uploads roughly 30 days apart -> median gap ~30 days, threshold ~90 days.
        var timestamps = new[] { Day(0), Day(30), Day(60), Day(90), Day(120) };

        Assert.False(UploadStalenessCalculator.IsStale(timestamps, Day(120 + 89)));
        Assert.True(UploadStalenessCalculator.IsStale(timestamps, Day(120 + 91)));
    }
}
