# Design

## Access pattern — no GSI needed

Reports are stored `PK = PROJECT#<projectId>`, `SK = CASEREPORT#<uploadedAt "O">#<id>`
(same for flag-proof: `SK = FLAGPROOF#<uploadedAt>#<id>`). A windowed read for one project
is a native DynamoDB `Query` on the project partition with an `SK BETWEEN
CASEREPORT#<from> AND CASEREPORT#<to>` — no new index.

The org rollup queries each of the organization's projects (an org has a handful of
projects; the project list is already loaded for the dashboard) and merges in memory. No
scan, no GSI, no precomputed rollup table. If an org ever has enough projects/reports that
fan-out is slow, revisit with a `ORG#<id> → uploadedAt` GSI then — not now.

New repository method: `ListByProjectInRangeAsync(projectId, from, to)` on both
`CaseReportRepository` and `FlagProofReportRepository` (the existing `ListByProjectAsync`
becomes a call with an open range, or stays and the new one is added alongside).

## Bucketing

- window `7d` / `30d` → **daily** buckets, boundary at UTC midnight
- window `90d` → **weekly** buckets (ISO week, Monday start, UTC)
- Buckets with no runs are emitted as zero-value points so the chart x-axis is continuous.
- A report is assigned to the bucket its `UploadedAt` falls in.

## Series per bucket

| Series | Numerator / denominator |
|---|---|
| case pass rate | reports with outcome `passed` / all case reports in bucket |
| flag-proof pass rate | flag-proof reports `proven` / flag-proof reports that were `eligible` (exclude `ineligible`) |
| run volume | count of case reports + flag-proof reports |
| classification breakdown | count per `FailureClassification` value among failed case reports |

Rates are `null` (rendered as a gap) when the denominator is zero — not `0%`, which would
be misleading.

## Flakiest cases

For the window, group case reports by `CaseId`, order each group by `UploadedAt`, count
transitions where `passed` differs from the previous report's `passed`. Return the top N
(N = 5) by transition count, tie-broken by most recent activity. A case that only ever
passed or only ever failed has zero flips and never appears.

## Entitlement

`TrendEndpoints` calls `IEntitlementService.For(org)` and returns
`EntitlementRequiredException`'s HTTP shape (from `plan-catalog-and-entitlements`) when
`!TrendAnalytics`. The dashboard checks the entitlement it already receives in its
bootstrap DTO and renders the upgrade prompt without calling the endpoint.

## Charting dependency

`web/` has no chart library today. Options: (a) hand-rolled SVG line/bar (dependency-free,
~150 lines, fine for four simple charts), (b) add a small lib. Proposed: **(a)** for this
change — the charts are simple and a dependency is a supply-chain + bundle cost that needs
its own justification. Note it in the PR if that changes.
