## 1. Prerequisite

- [x] 1.1 Confirm `billing-integration` is landed on `main` — `BillingReconciliationService`, `Organization.BillingStatus` / grace timestamp / `PolarSubscriptionId`, and the `billing_reconciliation` Lambda all present. Do not start otherwise.

## 2. Snapshot types and config

- [x] 2.1 Add a `BillingMetrics` options class bound from an `appsettings` section: `SpikeMultiplier` (default 5), `AnomalyLookbackDays` (default 28), `FreeTierVolumeThreshold` (default TBD from design D6), with documented defaults in `appsettings.json`.
- [x] 2.2 Define `BillingMetricsSnapshot` and its row records: `QuantityDriftRow` (orgId, name, billedQuantity, actualProjectCount, disposition ∈ {Applied, Simulated}, overBilled bool), `GraceRow` (orgId, name, status, daysElapsed, daysRemaining, lapsed bool), `ReadOnlyRow` (orgId, name, writableCount, readOnlyCount), `CounterIntegrityRow` (orgId, name, counterValue, storedRowCount, difference), `VolumeAnomalyRow` (orgId, name, kind ∈ {Spike, GoneQuiet}, currentRate, trailingRate), `FreeTierVolumeRow` (orgId, name, periodVolume). Add `IsEmpty`.

## 3. Checks — `BillingMetricsCollector`

- [x] 3.1 Quantity-drift row: from the reconciliation loop's existing per-org `subscription.Quantity` vs `Math.Max(projectCount,1)` comparison, plus `PolarOptions.ReconciliationDryRun` → disposition, and `billedQuantity > actualProjectCount` → `overBilled`. Orgs with no `PolarSubscriptionId` produce no row.
- [x] 3.2 Grace row: for orgs with `BillingStatus != active`, compute days elapsed/remaining against the 14-day window from the billing grace timestamp (reuse `billing`'s grace constant, do not redefine it); `lapsed` when `now > since + 14d`.
- [x] 3.3 Read-only row: for orgs where `projectCount > EffectiveMaxProjects(org)`, emit the `ProjectWritabilityService.WritableProjectIds` split.
- [x] 3.4 Counter-integrity row: for the current period, `UsageCounter` sum vs an independent count of stored case + flag-proof rows via `ListByProjectInRangeAsync` over the org's projects for `[periodStart, periodEnd)`; emit a row only on mismatch.
- [x] 3.5 Volume-anomaly rows: trailing rate over `AnomalyLookbackDays` ending at period start vs current-period rate per elapsed day; `Spike` when `current > SpikeMultiplier × trailing`; `GoneQuiet` when trailing ≥ 1/day and current-period uploads == 0 and period is ≥ 3 days in.
- [x] 3.6 Free-tier-volume row: free-tier orgs whose current-period case + flag-proof volume exceeds `FreeTierVolumeThreshold`.
- [x] 3.7 Unit-test each check in isolation against in-memory repository fakes (follow `StalenessDigestServiceTests` patterns): drift applied vs simulated vs over-billed vs none; grace within vs lapsed; over-limit split; counter match vs mismatch; spike vs gone-quiet vs normal; free-tier over vs under threshold.

## 4. Digest formatting and wiring

- [x] 4.1 `BillingMetricsDigest.Format(BillingMetricsSnapshot)` → `(subject, body)`: one section per row type, each omitted when empty; subject names the dominant condition and total org count.
- [x] 4.2 Inject `IOperatorAlertPublisher` and `BillingMetricsCollector` into `BillingReconciliationService`; build the snapshot during the existing org iteration; after the loop, `PublishAsync` when `!snapshot.IsEmpty`.
- [x] 4.3 Always emit a `billing_metrics_digest_run` structured log line with per-section counts, whether or not an email is sent.
- [x] 4.4 Verify `SnsOperatorAlertPublisher`'s unset-ARN path: run completes, logs findings, sends nothing, does not throw (add/confirm a test).
- [x] 4.5 DI registration for `BillingMetricsCollector` and `BillingMetrics` options in `Program.cs`.

## 5. Infrastructure

- [x] 5.1 In `hosted/terraform/billing.tf`, add an `sns:Publish` statement scoped to the operator topic ARN to `aws_iam_role.billing_reconciliation` (reference the topic the same way `alerting.tf` exposes it). No new function, rule, or schedule.

## 6. Verification and docs

- [x] 6.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count.
- [x] 6.2 `openspec validate billing-metrics-digest --strict` passes.
- [x] 6.3 Note in `docs/billing.md` (or the billing runbook) that the nightly reconciliation run now also emails an operator digest, what each section means, and that "simulated only" drift is expected until reconciliation leaves dry-run.
- [ ] 6.4 Manual (needs a real AWS session — leave unchecked, surface it): after CI apply, trigger the `billing_reconciliation` Lambda once and confirm a digest email arrives (or a clean-run log line if nothing is flagged).
