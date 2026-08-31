## Why

`billing-integration` bills flat **per project** — Polar subscription quantity tracks each org's live project count. Its nightly `BillingReconciliationService` already detects drift between Polar's quantity and our project count, **but it only writes a log line and then silently corrects Polar toward our number**. If *our* count is the wrong one (an orphaned project row, a bug in the create/delete quantity sync, a race), we mis-bill and nobody finds out. More broadly, there is no operator-facing signal for any billing-integrity or abuse condition: grace-window parking, read-only-enforcement gaps, a runaway CI loop about to trigger a surprise mid-month proration, or a free-tier org quietly running a fleet. The alerting channel to carry such a signal already exists (`IOperatorAlertPublisher` → SNS → operator email, from the archived `operator-alerting` change) — nothing in the billing path uses it.

`usage-metering`'s per-org report counter is **not** a billing input (metered billing is an explicit non-goal), but it is a cheap integrity canary: a divergence between the stored counter and the actual count of stored report rows means the ingest path has a counting bug — the same class of bug that would matter immediately if usage billing is ever added.

## What Changes

- Extend the existing nightly billing job (the `BillingReconciliation` scheduled Lambda — **no new Lambda, no new schedule, no new infra**) so that after it runs it composes **one operator digest email** via the existing `IOperatorAlertPublisher`, sent only when at least one monitored condition is present. The job already scans every organization; the digest adds a small number of extra reads.
- The digest reports, per organization where applicable:
  - **Subscription-quantity drift** — Polar quantity vs actual project count, and for each drift whether the reconciliation job **applied** the correction or only **simulated** it (dry-run). A drift toward *fewer* real projects than billed (customer over-charged) is called out distinctly from drift the other way.
  - **Billing-status state** — orgs in `past_due`, how many days into the 14-day grace window, and orgs where grace has lapsed to effective-Free.
  - **Read-only enforcement** — orgs whose project count exceeds their tier limit, with the writable/read-only split.
  - **Usage-counter integrity** — for the current period, the stored `UsageCounter` value vs an independent count of stored case/flag-proof report rows for that org; any mismatch is flagged as a suspected ingest-path counting bug.
  - **Volume anomaly** — current-period upload rate vs the org's own trailing multi-week average; large spikes (probable misconfigured CI loop — reach out before the proration lands) and drops to zero after sustained activity.
  - **Free-tier volume** — free-tier orgs above a soft threshold (conversion signal and abuse signal at once).
- No dedup / "already notified" state — a persistent condition reappears in each day's digest, matching `StalenessDigestService`'s deliberate choice.
- Thresholds (grace-window days is owned by `billing`; spike multiplier, free-tier volume, anomaly lookback) are configuration with sane defaults. Missing alerting config degrades to a warning log, never a crash — matching `SnsOperatorAlertPublisher`.
- Structure the checks as a `BillingMetricsSnapshot` of typed rows that the digest merely formats, so a later read-only operator endpoint can render the same rows without rework.

### Explicitly deferred (not in this change)

- **Real-time tripwire** on the ingest or project-create path (alert the instant an org crosses a threshold). Deferred until it is shown that a once-daily latency actually misses something costly — it adds latency and a failure mode to a hot path.
- **Operator web app / dashboard.** Email is the right latency for a one-operator pre-pilot; the `BillingMetricsSnapshot` shape keeps the door open.
- **Automated enforcement** — throttling, suspension, forcing a downgrade. The digest informs a human; the human acts (a manual operator downgrade already exists).
- **Changing what the reconciliation job does** — it still corrects Polar toward our project count. This change only makes its findings, and a few adjacent signals, visible.
- **Metered / usage-based billing** of any kind. Out of scope, unchanged non-goal.

## Capabilities

### New Capabilities

- `billing-metrics-digest`: A once-daily operator-facing digest of billing-integrity and abuse signals — subscription-quantity drift (and whether it was corrected or simulated), billing-status grace and read-only-enforcement state, usage-counter-vs-stored-rows integrity, and per-organization volume anomalies — delivered as a single email through the existing operator alert channel, carrying no automated enforcement and mutating no customer state.

### Modified Capabilities

- None. `billing`'s reconciliation requirement ("SHALL log every correction") is unchanged — this capability adds an operator-notification behavior alongside it, it does not alter the job's billing actions. `usage-metering` is read and cross-checked, not modified.

## Impact

- **Depends on** `billing-integration` landing first (this extends its `BillingReconciliationService` and reads `Organization.BillingStatus` / `BillingStatusSince` / `PolarSubscriptionId`).
- **New code** (BSL / `hosted/`): `BillingMetricsSnapshot` + the checks that build it (drift classification, usage-counter reconciliation, per-org anomaly), digest formatting, and wiring `IOperatorAlertPublisher` into the reconciliation Lambda's run. Unit tests against in-memory fakes, following `StalenessDigestService`'s test pattern.
- **Reuses unchanged**: `IOperatorAlertPublisher` / the operator SNS topic, `IOrganizationRepository.ListAllAsync`, `IProjectRepository.ListByOrganizationAsync`, `IUsageCounterRepository.GetAsync`, the case/flag-proof report repositories, `ProjectWritabilityService`, `Keys.CurrentUtcPeriod()`.
- **Infra (Terraform, CI-only apply)**: one IAM statement — `sns:Publish` to the operator topic — added to the existing `billing_reconciliation` Lambda role in `hosted/terraform/billing.tf`. No new function, rule, table, or secret.
- **No changes** to the ingest hot path, the webhook, the public API, the dashboard, or the marketing site.
- **Manual step**: none — the operator SNS topic and its email subscription already exist from `operator-alerting`.
