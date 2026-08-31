## Why

The dashboard shows a flat list of uploaded runs (`dashboard` capability: "Run history is
visible"). A team proving fixes over weeks has no way to see whether things are getting
better or worse — is the flag-proof pass rate climbing, is a particular case flaky, is run
volume dropping (a sign the CI wiring broke). That trend view is the first hosted feature
with real recurring value on top of the free CLI, and it needs **no new uploaded data** —
every report already carries an outcome, a classification, a flag-proof result, and a
timestamp.

`docs/` (planning repo) lists trend analytics as a Team-tier value-add. This change adds
it, gated by the `trendAnalytics` entitlement from `plan-catalog-and-entitlements`.

## What Changes

- **A read-only analytics API** on `ReleaseTwin.Hosted.Api`, scoped like the rest of the
  dashboard (authenticated web session, caller's organization only):
  - `GET /projects/{id}/trends?window=7d|30d|90d` — for one project
  - `GET /trends?window=…` — organization rollup across all projects
  - Response is a set of time-bucketed series (daily buckets for 7d/30d, weekly for 90d):
    - case pass rate (passed / total) per bucket
    - flag-proof pass rate (proven / eligible) per bucket
    - run volume (count) per bucket
    - failure-classification breakdown (counts per classification) per bucket
  - Plus a small "flakiest cases" list for the window: cases whose outcome flipped
    pass↔fail most often, with the flip count.

- **Computation** is a query + in-memory aggregation over the existing report items for the
  window. No precomputed rollups, no new write path. See design.md for the access pattern
  and the one possible GSI.

- **Entitlement gate.** The endpoints require `entitlements.TrendAnalytics`. Without it
  the API returns the same "entitlement required" shape the evidence endpoints use, and
  the dashboard shows an upgrade prompt in place of the charts.

- **Dashboard views** (`web/`): a "Trends" tab on the project page and on the org
  dashboard — line charts for the rates and volume, a stacked bar for the classification
  breakdown, and the flakiest-cases list. Charts use a dependency-free lightweight
  renderer or an already-present chart lib (decide in implementation; no new heavy dep
  without noting it).

## Capabilities

### Added Capabilities

- `trend-analytics`: time-bucketed pass-rate, flag-proof-rate, run-volume, and
  failure-classification series over a selectable window, per project and per
  organization, plus a flakiest-cases list; derived from existing report metadata and
  gated by the `trendAnalytics` entitlement.

### Modified Capabilities

- `dashboard`: adds a trends view for entitled organizations; unentitled organizations see
  an upgrade prompt in its place.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/` — `Analytics/TrendService.cs`, `Analytics/TrendEndpoints.cs`,
  DTOs; a repository method to read reports for a project/org within a time range.
- `hosted/ReleaseTwin.Hosted.Api.Tests/` — `TrendServiceTests` (bucketing boundaries,
  empty windows, flip counting, classification sums), endpoint auth + entitlement tests.
- `web/src/app/(dashboard)/…` — trends tab(s), chart components.
- DynamoDB: reports are already stored per project; a time-range read may want a GSI on
  `project → uploadedAt` if the current key schema doesn't support it efficiently —
  design.md. No new table.
- **Depends on** `plan-catalog-and-entitlements` for the entitlement.
- **No change** to the ingest contract, the CLI, or `ReleaseTwin.Core`.

## Open Questions

- Bucket granularity: fixed (daily ≤30d, weekly for 90d) as proposed, vs a `bucket` query
  param. Proposed: **fixed** — fewer states to test, matches how the charts read.
- "Flakiest cases" definition: pass↔fail flip count (proposed) vs a stddev/entropy score.
  Proposed: **flip count** — legible, and a team knows what to do with it.
- Timezone for day buckets: UTC (proposed) vs the org's locale. Proposed: **UTC** — no
  per-org timezone is stored today; revisit if asked.
- Do we need a GSI, or can we scan-and-filter within an org's partition? Depends on the
  current report key layout — resolve in design before implementation, prefer adding a GSI
  over a scan.
