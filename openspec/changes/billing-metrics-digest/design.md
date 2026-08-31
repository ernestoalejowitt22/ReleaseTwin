## Context

See `proposal.md` for motivation. Relevant current state (post `billing-integration`):

- `BillingReconciliationService.RunAsync()` (`hosted/ReleaseTwin.Hosted.Api/Billing/`) already runs nightly as the `RELEASETWIN_LAMBDA_TASK=BillingReconciliation` scheduled Lambda (`hosted/terraform/billing.tf`), scans every org via `IOrganizationRepository.ListAllAsync`, and for each org with a `PolarSubscriptionId` compares `subscription.Quantity` to `Math.Max(projectCount, 1)`, corrects Polar toward the project count unless `PolarOptions.ReconciliationDryRun`, and logs `billing_reconciliation_correction` / `billing_reconciliation_run`. It returns a `Result(OrgsChecked, CorrectionsMade, Skipped)` that `Program.cs` discards.
- `ProjectWritabilityService.WritableProjectIds(projects, effectiveMax)` is the single resolver for the writable/read-only split; `EffectiveMaxProjects(org)` already folds in billing-status degradation.
- `Organization` carries `BillingStatus`, `BillingStatusSince` (or equivalent timestamp for the 14-day grace math — `billing` D4), `BillingCadence`, `PolarCustomerId`, `PolarSubscriptionId`.
- `IUsageCounterRepository.GetAsync(orgId, period)` returns a never-null counter with `CaseReportCount` / `FlagProofReportCount`. `ICaseReportRepository` / `IFlagProofReportRepository` expose `ListByProjectAsync` and `ListByProjectInRangeAsync(projectId, from, to)`.
- `IOperatorAlertPublisher.PublishAsync(subject, message)` → SNS topic `Alerting:OperatorTopicArn`; `SnsOperatorAlertPublisher` no-ops with a warning log when the ARN is unset. `StalenessDigestService` is the reference implementation for "scan all orgs, build text lines, publish one digest, no dedup state."
- The `billing_reconciliation` Lambda's IAM role currently grants `dynamodb:Query` + `dynamodb:Scan` only — no `sns:Publish`.

## Goals / Non-Goals

**Goals:**

- Every billing-integrity signal the nightly job already has in hand (drift, correction disposition, read-only split) reaches a human, plus a few adjacent signals cheap to compute in the same pass (usage-counter integrity, volume anomaly, free-tier volume).
- Zero new AWS resources: one IAM statement added to an existing role, everything else is code in an already-scheduled job.
- The checks are a pure, testable transform over repository reads — `BillingMetricsSnapshot` — that the digest only formats.

**Non-Goals:**

- Changing any billing action the reconciliation job takes (still corrects Polar toward our project count).
- A second scheduled job or a second cadence. If billing-integrity ever needs sub-day latency, that is a real-time tripwire on the write path, designed separately.
- Persisting snapshots or "last notified" state anywhere.

## Decisions

### D1: Extend the existing reconciliation job, do not add a `BillingMetricsDigest` Lambda

The nightly `BillingReconciliation` function already does the expensive part — a full-org scan with per-org project lists and Polar quantity reads. A standalone digest Lambda would repeat that scan on its own schedule for no benefit and add a function + EventBridge rule + IAM role to maintain.

- Implementation shape: `BillingReconciliationService` gains an injected `IOperatorAlertPublisher` and an injected `BillingMetricsCollector` (the new checks). `RunAsync` builds a `BillingMetricsSnapshot` as it already iterates orgs, and after the loop calls a `BillingMetricsDigest.Format(snapshot)` → `PublishAsync` when the snapshot is non-empty. The existing `Result` record is extended or wrapped; `Program.cs`'s branch is unchanged (still `GetRequiredService<BillingReconciliationService>().RunAsync()`).
- **Alternative — a 5th `RELEASETWIN_LAMBDA_TASK` branch sharing the artifact** (the pattern staleness/purge/reconciliation all use): consistent, but here the data source and cadence are identical to an existing job, so sharing the *job* beats sharing only the *artifact*. Rejected.
- **Alternative — fold this into `billing-integration` before it archives**: `billing-integration` is mid-migration (dry-run, upgrade button off); freezing its scope is worth a separate change. The dependency is one-directional and clean.

### D2: The digest is composed from a `BillingMetricsSnapshot` of typed rows, not built inline as strings

`StalenessDigestService` builds `staleLines` as strings inline. This capability has six distinct check types with structure (before/after quantities, day counts, split counts) and a likely future second renderer (an operator endpoint). So the checks produce typed rows (`QuantityDriftRow`, `GraceRow`, `ReadOnlyRow`, `CounterIntegrityRow`, `VolumeAnomalyRow`, `FreeTierVolumeRow`) collected in `BillingMetricsSnapshot`; `BillingMetricsDigest.Format` turns them into the email body; `snapshot.IsEmpty` gates sending.

- **Alternative — inline strings like the staleness digest**: fine for one check type, awkward for six with a future consumer. Rejected.

### D3: Send only on signal, always log the run

Matches `StalenessDigestService` (no email when `staleLines.Count == 0`). The run always emits a structured completion log line (`billing_metrics_digest_run` with per-section counts) so "did it run" is answerable from CloudWatch regardless of whether an email went out. No weekly "all clear" heartbeat in Phase 1 — the reconciliation job's own `billing_reconciliation_run` line and the API 5xx alarm already prove the schedule is alive.

### D4: No dedup / "already notified" state

Same deliberate choice as `StalenessDigestService`. A drift that persists for a week is in the digest every day that week — that repetition *is* the escalation for a solo operator. Suppression state would be the first thing to rot.

### D5: Usage-counter integrity check reuses per-project range queries, capped

For each org, sum `CaseReportCount + FlagProofReportCount` from the counter, then independently count stored rows for the period by iterating the org's projects and calling `ListByProjectInRangeAsync(projectId, periodStart, periodEnd)`. At current scale (single-digit customers, low-double-digit projects) this is a handful of native partition range queries per org per night. If project counts ever make this heavy, the check can be sampled (every Nth org per night) without changing the spec — the spec says "the digest SHALL compare", not "every night for every org". Documented as a known scaling lever, not built now.

### D6: Volume-anomaly baseline is the org's own trailing average, computed from the same range queries

Trailing window default 28 days, ending at period start. Rate = uploads per day over that window; current rate = current-period uploads per elapsed day of the period. Spike = current > `SpikeMultiplier` × trailing (default 5). "Gone quiet" = trailing rate ≥ 1/day sustained and current-period uploads == 0 and the period is at least a few days in (avoid firing on the 1st of the month). Thresholds in `appsettings` under a `BillingMetrics` section.

- This deliberately mirrors `UploadStalenessCalculator`'s "relative to the project's own cadence" philosophy rather than any absolute number — a 10-upload/day project and a 1000-upload/day project both get a meaningful signal.

### D7: IAM — add `sns:Publish` for the operator topic to the reconciliation role only

One statement in `hosted/terraform/billing.tf` on `aws_iam_role.billing_reconciliation`, scoped to the operator topic ARN (referenced the same way `alerting.tf` exposes it). No change to the HTTP function's role.

## Risks / Trade-offs

- **The reconciliation job now depends on the alerting topic ARN being wired** → `SnsOperatorAlertPublisher` already no-ops with a warning when unset (spec requirement), so a missing ARN degrades to "job runs, logs findings, sends nothing" — not a failure.
- **Extra DynamoDB reads per night on the reconciliation function** → bounded by D5's sampling lever; at present scale it is a few dozen range queries. The function timeout is already 60s for a full scan + Polar round-trips.
- **A noisy digest during `billing-integration`'s dry-run period** (every real drift shows as "simulated only") → that is the intended signal for exactly that window; once reconciliation flips out of dry-run (billing migration step 6) the simulated entries become applied entries and the volume drops to genuine anomalies.
- **Anomaly false positives from legitimate CI-adoption ramps** → the digest is advisory and addressed to one person who can dismiss a line in a second; no enforcement rides on it. Acceptable.
- **Snapshot computed mid-scan could reflect a project created during the run** → the digest is a daily approximation, not an accounting record; a one-run-late entry self-corrects the next night. Acceptable.

## Migration Plan

1. Land `billing-integration` (prerequisite).
2. Ship the code: `BillingMetricsCollector`, `BillingMetricsSnapshot` + rows, `BillingMetricsDigest.Format`, `IOperatorAlertPublisher` injected into `BillingReconciliationService`, DI registration, `BillingMetrics` config section with defaults.
3. Terraform: add the `sns:Publish` statement to the `billing_reconciliation` role. CI apply.
4. First nightly run after deploy: expect a digest dominated by "simulated only" drift entries while reconciliation is still in dry-run — this is the baseline.
5. No flip, no flag — the digest is on as soon as the code and IAM are deployed. To silence it, unset the operator topic ARN (degrades to log-only) or disable the reconciliation schedule.

**Rollback**: revert the `IOperatorAlertPublisher` call in `BillingReconciliationService` (the collector and snapshot types are inert without it); the IAM statement can stay.

## Open Questions

1. **Trailing-window default — 28 days vs 14** for the volume-anomaly baseline. Deferrable: it is a single `appsettings` default (D6), changing it alters no spec scenario and no task.
2. **Whether over-billing drift (customer over-charged) should also page immediately** rather than wait for the daily digest. Deferrable: it is the strongest candidate for the deferred real-time tripwire, but only meaningful once reconciliation is out of dry-run and real subscriptions exist; revisit when the first paying cohort is on.
