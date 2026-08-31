## Why

A team runs many cases across a release. Today the dashboard answers "did this case pass"
one row at a time; it cannot answer the question that actually gates a ship decision:
**"is release 4.2 proven?"** — how many of the cases tied to that release have a current
known-good result, how many are failing, how many were never run.

The cases already know which release they belong to — it's in the tracker ticket the
oracle points at, or the sprint. This change lets a case declare a `release` label, carries
it through upload, and adds a rollup view. Team-tier, gated by the `releaseRollup`
entitlement from `plan-catalog-and-entitlements`.

## What Changes

- **Case file: optional `release` field.**

  ```yaml
  id: MY-CASE-1
  release: "4.2"          # optional, free-form short string
  oracle: { locator: tickets/MY-CASE-1 }
  fixture: { locator: my-fixture.json, sha256: ... }
  pipeline: [ ... ]
  ```

  `release` is a free-form label (a version, a sprint name, an epic key). `case-loading`
  parses it into the core case model as an optional string. It does not affect execution,
  eligibility, or exit code — it's a grouping tag.

- **Ingest carries it through.** When the CLI uploads a report, it includes the case's
  `release` label if set. New optional `release` string on the ingest payload's core
  metadata — a short opaque identifier, no more sensitive than the case id it sits next to.
  A report with no `release` is accepted exactly as today.

- **Rollup API + view.** `GET /projects/{id}/releases` lists the release labels seen in
  that project's reports. `GET /projects/{id}/releases/{label}` returns the readiness
  rollup for one release:
  - each case that has ever reported under that label, with its **latest** result
    (passed / failed / flag-proof proven / never-run-recently)
  - counts: proven, failing, stale/never-run
  - a single headline state: **Proven** (all cases proven or passed), **Not proven**
    (any failing), **Incomplete** (some cases have no recent run)
  - "recent" window is configurable per request, default 14 days — a case whose last run
    is older than the window counts as stale.

- **Entitlement gate** on `releaseRollup`. Unentitled orgs see an upgrade prompt where the
  release view would be.

- **Dashboard**: a "Releases" section on the project page — the label list, and the
  per-release rollup with the headline state and the case table.

## Capabilities

### Added Capabilities

- `release-rollup`: aggregates a project's case and flag-proof reports by a case-declared
  `release` label into a per-release readiness view (latest result per case, proven /
  failing / stale counts, and a headline Proven / Not proven / Incomplete state), gated by
  the `releaseRollup` entitlement.

### Modified Capabilities

- `case-loading`: the case model gains an optional free-form `release` label that does not
  affect execution.
- `ingest-api`: the report payload gains an optional `release` string carried from the
  case; absence is unchanged behavior.
- `dashboard`: adds a per-project Releases view for entitled organizations.

## Impact

- `src/ReleaseTwin.Core/` — optional `Release` string on the case model; the loader in
  `ReleaseTwin.Cli` / `case-loading` populates it.
- `src/ReleaseTwin.Cli/` — include `release` in the uploaded report DTO.
- `hosted/ReleaseTwin.Hosted.Api/` — `release` on `UploadedCaseReport` /
  `UploadedFlagProofReport` + item attribute; `Releases/ReleaseRollupService.cs` +
  endpoints; DTOs.
- `hosted/ReleaseTwin.Hosted.Api.Tests/` — ingest with/without `release`; rollup service
  (latest-per-case, headline state, stale window); endpoint auth + entitlement tests.
- `web/` — Releases section + rollup view.
- `examples/`, `docs/quickstart.md`, `README.md` — show the `release` field.
- DynamoDB: `release` is a new attribute on existing report items; the rollup reads a
  project's reports (native partition query, same as `trend-analytics`) and groups in
  memory. No new table, no GSI.
- **Depends on** `plan-catalog-and-entitlements`.

## Open Questions

- `release` source: case-file field only (proposed, per your decision) — no dashboard-side
  tagging in v1.
- Multiple releases per case (a fix that spans two releases): single string in v1
  (proposed); a list is a later change if asked.
- Headline state when a release has zero cases with recent runs: **Incomplete** (proposed)
  vs a distinct "No data".
- "latest result per case" across both case reports and flag-proof reports — proposed:
  take the most recent report of either kind for that (case id, release); a flag-proof
  "proven" and a plain "passed" both count as green, "ineligible" counts as stale.
