## Context

See proposal.md — Why. The dashboard can answer "did this case pass" but not "is release
4.2 proven". Cases already know their release (it's in the tracker ticket or the sprint);
this change threads a free-form `release` label from the case file through upload into
storage, and adds a per-release readiness rollup.

Constraints that shape the approach:

- Reports are stored `PK = PROJECT#<projectId>`, `SK = CASEREPORT#<uploadedAt "O">#<id>` /
  `FLAGPROOF#<uploadedAt>#<id>` — the same layout `trend-analytics` just used for a
  windowed project read.
- The ingest contract is stable and metadata-only (`UploadedCaseReport` is a mirror of
  `ReleaseTwin.Core.CaseReport`, never fixture content). A new field has to keep that
  guarantee.
- `plan-catalog-and-entitlements` already defines `releaseRollup` on the Team/Enterprise
  tiers and the `EntitlementRequiredException` / `{ error: "entitlement-required" }` shape.

## Goals / Non-Goals

**Goals:**

- One optional string carried end to end (case file → core model → CLI DTO → ingest →
  report item) with absence behaving exactly as today.
- A rollup computed from data already stored, with no new write path, no new table, no GSI.
- Reuse the `trend-analytics` shapes: web-session-auth endpoint group, org scope + project
  ownership check, entitlement gate returning the standard shape, bootstrap-DTO-driven
  upgrade prompt.

**Non-Goals:**

- Dashboard-side release tagging (D1 — case-file field only in v1).
- Multiple releases per case (D2 — single string; a list is a later change).
- A release/label GSI or any precomputed rollup.
- Changing execution, eligibility, flag-proof behavior, or exit code — `release` is inert.

## Decisions

### D-A: `release` is a nullable attribute on the existing report items, grouped in memory

The rollup reads a project's case + flag-proof reports with the existing
`ListByProjectAsync` (native partition Query) and groups by `Release` then `CaseId` in
memory — identical scale rationale to `trend-analytics` (a table sized for a handful of
customers; an org has a handful of projects). `release` is written as an optional item
attribute (`Attrs.SOrNull` / `GetSOrNull`, same as `Classification`).

*Alternative rejected:* a `RELEASE#<label>` GSI. It buys nothing at this scale and adds an
index to provision and keep in sync; revisit only if a single project accumulates enough
reports that the partition read is slow.

### D-B: latest result per case = most recent report of either kind

For each `(release, caseId)`, take the single most recent report by `UploadedAt` across
both case reports and flag-proof reports (D4). Classify that latest report:

| Latest report | Classification |
|---|---|
| case report, `Passed = true` | green |
| flag-proof, `Outcome = Passed` | green |
| case report, `Passed = false` | failing |
| flag-proof, `Outcome ∈ { WeakOracle, BothFailed, Inverted }` | failing |
| flag-proof, `Outcome = Ineligible` | stale |
| any of the above, but `UploadedAt` older than the recency window | stale |

The window check is applied last: an old "green" report is stale, because readiness is a
statement about *now*.

### D-C: headline state precedence — failing > stale > proven

- **Not proven** — at least one case is failing.
- **Incomplete** — no case is failing and at least one is stale, *or* the release has no
  case with a recent run at all (D3 — Incomplete, not a distinct "No data").
- **Proven** — every case under the label is green.

### D-D: recency window is a validated request param, default 14 days

`window` accepts `7d` / `14d` / `30d` / `90d` (default `14d`) — a fixed allowlist, same
validation style as `trend-analytics`'s `window`. Anything else is a 400.

### D-E: ingest caps `release` length

The ingest endpoint rejects a `release` longer than 200 characters, the same defensive
bound the case id already carries — it is an opaque grouping identifier, not free text.

## Risks / Trade-offs

- **Label drift / typos fragment a release** (`4.2` vs `4.2.0`) → the releases-list view
  shows every distinct label, so a stray one is visible immediately; no silver bullet in
  v1, and a case-file field (reviewed in PRs) is less error-prone than ad-hoc UI tagging.
- **A case dropped from a release still appears under the old label** until its last report
  ages past the window → acceptable; it shows as stale, which is the honest state.
- **"Latest of either kind" can flip green↔stale as an `ineligible` flag-proof lands after
  a passing case report** → intended: an ineligible flag-proof is real signal that the
  oracle can't currently be proven.

## Migration Plan

Purely additive. The new attribute is written only when a case declares `release`; existing
report items keep no `release` and simply never appear in a rollup. No backfill, no data
migration. Rollback is removing the endpoints and the field readers — stored `release`
values become inert.
