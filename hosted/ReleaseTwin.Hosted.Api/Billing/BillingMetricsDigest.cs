using System.Globalization;
using System.Text;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing-metrics-digest: turns a non-empty <see cref="BillingMetricsSnapshot"/> into the operator
/// email. One section per row type, each omitted when it has no rows. Plain text — the operator alert
/// channel (SNS → email) is plain text, same as <see cref="Services.StalenessDigestService"/>.
/// </summary>
public static class BillingMetricsDigest
{
    public static (string Subject, string Body) Format(BillingMetricsSnapshot snapshot)
    {
        var subject = BuildSubject(snapshot);
        var body = new StringBuilder();

        body.AppendLine($"Billing-integrity digest — {snapshot.TotalFlaggedOrganizations} organization(s) flagged.");
        body.AppendLine();

        AppendQuantityDrift(body, snapshot);
        AppendGrace(body, snapshot);
        AppendReadOnly(body, snapshot);
        AppendCounterIntegrity(body, snapshot);
        AppendVolumeAnomalies(body, snapshot);
        AppendFreeTierVolume(body, snapshot);

        return (subject, body.ToString().TrimEnd());
    }

    private static string BuildSubject(BillingMetricsSnapshot snapshot)
    {
        var count = snapshot.TotalFlaggedOrganizations;

        // Name the most billing-consequential condition present.
        string headline;
        if (snapshot.QuantityDrift.Any(r => r.OverBilled))
        {
            headline = "subscription over-billing";
        }
        else if (snapshot.QuantityDrift.Count > 0)
        {
            headline = "subscription-quantity drift";
        }
        else if (snapshot.CounterIntegrity.Count > 0)
        {
            headline = "usage-counter mismatch";
        }
        else if (snapshot.Grace.Count > 0)
        {
            headline = "billing-status grace";
        }
        else if (snapshot.ReadOnlyEnforcement.Count > 0)
        {
            headline = "read-only enforcement";
        }
        else if (snapshot.VolumeAnomalies.Count > 0)
        {
            headline = "upload-volume anomaly";
        }
        else
        {
            headline = "free-tier volume";
        }

        return $"ReleaseTwin billing: {headline} ({count} org(s))";
    }

    private static void AppendQuantityDrift(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.QuantityDrift.Count == 0)
        {
            return;
        }

        body.AppendLine($"## Subscription-quantity drift ({snapshot.QuantityDrift.Count})");
        foreach (var row in snapshot.QuantityDrift)
        {
            var direction = row.OverBilled ? "OVER-BILLED" : "under-billed";
            var disposition = row.Disposition == DriftDisposition.Applied ? "correction applied" : "simulated only (dry-run)";
            body.AppendLine(
                $"- {row.OrganizationName} ({row.OrganizationId}): billed {row.BilledQuantity}, actual {row.ActualProjectCount} projects — {direction}, {disposition}");
        }

        body.AppendLine();
    }

    private static void AppendGrace(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.Grace.Count == 0)
        {
            return;
        }

        body.AppendLine($"## Billing-status grace ({snapshot.Grace.Count})");
        foreach (var row in snapshot.Grace)
        {
            var state = row.Lapsed
                ? "grace lapsed — entitlements effectively Free"
                : $"{row.DaysElapsed}d into grace, {row.DaysRemaining}d remaining";
            body.AppendLine($"- {row.OrganizationName} ({row.OrganizationId}): {row.Status}, {state}");
        }

        body.AppendLine();
    }

    private static void AppendReadOnly(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.ReadOnlyEnforcement.Count == 0)
        {
            return;
        }

        body.AppendLine($"## Read-only enforcement ({snapshot.ReadOnlyEnforcement.Count})");
        foreach (var row in snapshot.ReadOnlyEnforcement)
        {
            body.AppendLine(
                $"- {row.OrganizationName} ({row.OrganizationId}): {row.WritableCount} writable, {row.ReadOnlyCount} read-only");
        }

        body.AppendLine();
    }

    private static void AppendCounterIntegrity(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.CounterIntegrity.Count == 0)
        {
            return;
        }

        body.AppendLine($"## Usage-counter integrity ({snapshot.CounterIntegrity.Count}) — suspected ingest-path counting bug");
        foreach (var row in snapshot.CounterIntegrity)
        {
            var sign = row.Difference > 0 ? "+" : string.Empty;
            body.AppendLine(
                $"- {row.OrganizationName} ({row.OrganizationId}): counter {row.CounterValue}, stored rows {row.StoredRowCount} (diff {sign}{row.Difference})");
        }

        body.AppendLine();
    }

    private static void AppendVolumeAnomalies(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.VolumeAnomalies.Count == 0)
        {
            return;
        }

        body.AppendLine($"## Upload-volume anomalies ({snapshot.VolumeAnomalies.Count})");
        foreach (var row in snapshot.VolumeAnomalies)
        {
            var kind = row.Kind == VolumeAnomalyKind.Spike ? "SPIKE" : "gone quiet";
            body.AppendLine(
                $"- {row.OrganizationName} ({row.OrganizationId}): {kind} — {Rate(row.CurrentRatePerDay)}/day now vs {Rate(row.TrailingRatePerDay)}/day trailing");
        }

        body.AppendLine();
    }

    private static void AppendFreeTierVolume(StringBuilder body, BillingMetricsSnapshot snapshot)
    {
        if (snapshot.FreeTierVolume.Count == 0)
        {
            return;
        }

        body.AppendLine($"## High-volume free-tier organizations ({snapshot.FreeTierVolume.Count})");
        foreach (var row in snapshot.FreeTierVolume)
        {
            body.AppendLine($"- {row.OrganizationName} ({row.OrganizationId}): {row.PeriodVolume} uploads this period");
        }

        body.AppendLine();
    }

    private static string Rate(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
