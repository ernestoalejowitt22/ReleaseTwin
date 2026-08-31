## Purpose

Gives the operator a once-daily, human-readable digest of billing-integrity and abuse signals — subscription-quantity drift, billing-status grace and read-only-enforcement state, usage-counter integrity, and per-organization volume anomalies — so that a metering bug or an abusive usage pattern is noticed by a person within a day, without any automated enforcement or a standing operator app.

## ADDED Requirements

### Requirement: A daily billing-integrity digest is delivered to the operator when any monitored condition is present
The hosted platform SHALL, once per day, evaluate the billing-integrity and abuse checks defined by this capability across every organization and SHALL deliver a single digest to the operator through the existing operator alert channel when one or more organizations match one or more of those checks. When no organization matches any check, the platform SHALL NOT send a digest, and SHALL emit a structured log line recording that the run completed with nothing to report.

#### Scenario: A monitored condition produces a digest
- **WHEN** the daily run finds at least one organization matching at least one monitored check
- **THEN** exactly one digest is sent to the operator alert channel, listing every matching organization and the check(s) it matched

#### Scenario: A clean run sends no email
- **WHEN** the daily run finds no organization matching any monitored check
- **THEN** no digest is sent, and a structured log line records that the run completed clean

#### Scenario: A persistent condition re-reports each day
- **WHEN** the same organization matches the same check on consecutive daily runs
- **THEN** it appears in the digest on each of those days, with no suppression of the repeat

#### Scenario: The digest never changes customer state
- **WHEN** the daily run evaluates and reports its checks
- **THEN** it makes no change to any organization's tier, billing status, project state, or subscription — it only reads and notifies

### Requirement: Subscription-quantity drift is reported with its correction disposition
For each organization with a Merchant-of-Record subscription, the digest SHALL report any mismatch between the Merchant-of-Record subscription quantity and the organization's actual project count, and SHALL state whether the reconciliation job applied the correction or only simulated it. Drift where the billed quantity exceeds the real project count (the customer is over-billed) SHALL be distinguished from drift in the other direction.

#### Scenario: Applied correction is reported
- **WHEN** the reconciliation job corrects a subscription quantity and is not in dry-run mode
- **THEN** the digest lists the organization, the before and after quantity, the actual project count, and marks the correction as applied

#### Scenario: Simulated correction is reported
- **WHEN** the reconciliation job detects drift while in dry-run mode
- **THEN** the digest lists the organization and the drift, and marks the correction as simulated only

#### Scenario: Over-billing drift is called out
- **WHEN** an organization's billed subscription quantity is greater than its actual project count
- **THEN** the digest flags that organization as over-billed, separately from organizations billed for fewer projects than they have

#### Scenario: Externally managed organizations are not reported as drift
- **WHEN** an organization has no Merchant-of-Record subscription
- **THEN** it never appears in the drift section of the digest

### Requirement: Billing-status grace and read-only enforcement state are reported
The digest SHALL list organizations in a non-active billing status, including how far each is through its grace window, and organizations whose project count exceeds their current tier's limit, including the writable-versus-read-only split.

#### Scenario: Past-due organization within grace is listed
- **WHEN** an organization's billing status is past-due and its grace window has not lapsed
- **THEN** the digest lists it with the number of days elapsed and remaining in the grace window

#### Scenario: Lapsed grace is listed
- **WHEN** an organization's grace window has lapsed and its entitlements are effectively reduced to Free
- **THEN** the digest lists it as lapsed-to-Free

#### Scenario: Over-limit organization shows the enforcement split
- **WHEN** an organization has more projects than its current tier allows
- **THEN** the digest lists it with the count of writable projects and the count of read-only projects

### Requirement: Usage-counter integrity is cross-checked against stored report rows
For the current period, the digest SHALL compare each organization's stored usage counter to an independent count of that organization's stored case-report and flag-proof-report rows for the same period, and SHALL flag any organization whose two values disagree as a suspected ingest-path counting bug.

#### Scenario: Counter matches stored rows
- **WHEN** an organization's stored usage counter equals the independent count of its stored report rows for the period
- **THEN** the organization does not appear in the integrity section

#### Scenario: Counter disagrees with stored rows
- **WHEN** an organization's stored usage counter differs from the independent count of its stored report rows for the period
- **THEN** the digest flags the organization with both values and the difference

### Requirement: Per-organization upload-volume anomalies are flagged
The digest SHALL compare each organization's current-period upload rate to that organization's own trailing multi-week average and SHALL flag large spikes and drops to zero after a period of sustained activity. Free-tier organizations whose current-period volume exceeds a configured soft threshold SHALL be listed.

#### Scenario: Spike is flagged
- **WHEN** an organization's current-period upload rate exceeds its trailing average by more than the configured spike multiplier
- **THEN** the digest flags the organization as a volume spike with both figures

#### Scenario: Activity stopping is flagged
- **WHEN** an organization had sustained upload activity and its current-period uploads have fallen to zero
- **THEN** the digest flags the organization as having gone quiet

#### Scenario: High-volume free-tier organization is listed
- **WHEN** a free-tier organization's current-period upload volume exceeds the configured soft threshold
- **THEN** the digest lists it in the free-tier-volume section

### Requirement: Monitored-condition thresholds are configurable and missing alert configuration degrades gracefully
The spike multiplier, the anomaly lookback window, and the free-tier volume threshold SHALL be configuration values with documented defaults. When the operator alert channel is not configured, the daily run SHALL complete its evaluation and record its findings in a structured log line without raising an error.

#### Scenario: Defaults apply when unset
- **WHEN** no threshold configuration is provided
- **THEN** the checks run with their documented default values

#### Scenario: No alert channel configured
- **WHEN** the operator alert channel has no destination configured and the daily run finds matching conditions
- **THEN** the run logs its findings and completes successfully without sending and without throwing
