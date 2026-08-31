using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>billing-metrics-digest: whether the reconciliation job applied the drift correction this run, or only simulated it (dry-run).</summary>
public enum DriftDisposition
{
    Applied,
    Simulated,
}

public enum VolumeAnomalyKind
{
    Spike,
    GoneQuiet,
}

/// <summary>A mismatch between the Merchant-of-Record subscription quantity and the org's actual project count.</summary>
public sealed record QuantityDriftRow(
    Guid OrganizationId,
    string OrganizationName,
    int BilledQuantity,
    int ActualProjectCount,
    DriftDisposition Disposition,
    bool OverBilled);

/// <summary>An org in a non-active billing status, with its position in the grace window.</summary>
public sealed record GraceRow(
    Guid OrganizationId,
    string OrganizationName,
    BillingStatus Status,
    int DaysElapsed,
    int DaysRemaining,
    bool Lapsed);

/// <summary>An org with more projects than its current effective tier allows.</summary>
public sealed record ReadOnlyRow(
    Guid OrganizationId,
    string OrganizationName,
    int WritableCount,
    int ReadOnlyCount);

/// <summary>The stored usage counter disagrees with an independent count of stored report rows for the period.</summary>
public sealed record CounterIntegrityRow(
    Guid OrganizationId,
    string OrganizationName,
    long CounterValue,
    long StoredRowCount,
    long Difference);

/// <summary>An org whose current-period upload rate is far from its own trailing average.</summary>
public sealed record VolumeAnomalyRow(
    Guid OrganizationId,
    string OrganizationName,
    VolumeAnomalyKind Kind,
    double CurrentRatePerDay,
    double TrailingRatePerDay);

/// <summary>A Free-tier org above the configured soft volume threshold.</summary>
public sealed record FreeTierVolumeRow(
    Guid OrganizationId,
    string OrganizationName,
    long PeriodVolume);

/// <summary>
/// billing-metrics-digest: the typed result of one nightly evaluation across every organization. The
/// digest formatter turns this into an email body; <see cref="IsEmpty"/> gates whether anything is
/// sent. Kept as structured rows (not pre-formatted strings) so a later read-only operator endpoint
/// can render the same data (design.md D2).
/// </summary>
public sealed class BillingMetricsSnapshot
{
    public List<QuantityDriftRow> QuantityDrift { get; } = [];
    public List<GraceRow> Grace { get; } = [];
    public List<ReadOnlyRow> ReadOnlyEnforcement { get; } = [];
    public List<CounterIntegrityRow> CounterIntegrity { get; } = [];
    public List<VolumeAnomalyRow> VolumeAnomalies { get; } = [];
    public List<FreeTierVolumeRow> FreeTierVolume { get; } = [];

    public bool IsEmpty =>
        QuantityDrift.Count == 0
        && Grace.Count == 0
        && ReadOnlyEnforcement.Count == 0
        && CounterIntegrity.Count == 0
        && VolumeAnomalies.Count == 0
        && FreeTierVolume.Count == 0;

    /// <summary>Distinct organizations appearing in at least one section.</summary>
    public int TotalFlaggedOrganizations =>
        QuantityDrift.Select(r => r.OrganizationId)
            .Concat(Grace.Select(r => r.OrganizationId))
            .Concat(ReadOnlyEnforcement.Select(r => r.OrganizationId))
            .Concat(CounterIntegrity.Select(r => r.OrganizationId))
            .Concat(VolumeAnomalies.Select(r => r.OrganizationId))
            .Concat(FreeTierVolume.Select(r => r.OrganizationId))
            .Distinct()
            .Count();
}
