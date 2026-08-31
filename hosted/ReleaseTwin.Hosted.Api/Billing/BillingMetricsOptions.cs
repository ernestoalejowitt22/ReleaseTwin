using Microsoft.Extensions.Configuration;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing-metrics-digest: thresholds for the nightly operator digest's anomaly checks. Bound inline
/// from the <c>BillingMetrics</c> configuration section (same pattern as <see cref="PolarOptions"/> —
/// no <c>IOptions&lt;T&gt;</c> ceremony). Every value has a documented default so the digest runs with
/// nothing configured.
/// </summary>
public sealed class BillingMetricsOptions
{
    public const string SectionName = "BillingMetrics";

    /// <summary>
    /// A current-period upload rate this many times the organization's own trailing average is
    /// flagged as a spike (design.md D6). Default 5.
    /// </summary>
    public double SpikeMultiplier { get; init; } = 5.0;

    /// <summary>
    /// The trailing window, in days ending at the current period's start, used as each organization's
    /// own baseline for the volume-anomaly check (design.md D6). Default 28.
    /// </summary>
    public int AnomalyLookbackDays { get; init; } = 28;

    /// <summary>
    /// A Free-tier organization whose current-period case + flag-proof upload count exceeds this is
    /// listed in the digest (conversion and abuse signal at once). Default 500.
    /// </summary>
    public long FreeTierVolumeThreshold { get; init; } = 500;

    public static BillingMetricsOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new BillingMetricsOptions
        {
            SpikeMultiplier = double.TryParse(section["SpikeMultiplier"], out var m) && m > 0 ? m : 5.0,
            AnomalyLookbackDays = int.TryParse(section["AnomalyLookbackDays"], out var d) && d > 0 ? d : 28,
            FreeTierVolumeThreshold = long.TryParse(section["FreeTierVolumeThreshold"], out var t) && t > 0 ? t : 500,
        };
    }
}
