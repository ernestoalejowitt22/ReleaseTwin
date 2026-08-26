## Context

`DashboardService.GetDashboardViewAsync` already loads `caseReports` and `flagProofReports` for the
selected project (each report carries an `UploadedAt`) and shapes them into a `DashboardView`. There
is no existing notion of a project's "normal cadence" anywhere in the data model — `Project` carries
no denormalized last-upload or cadence field. See proposal.md for why this is being added.

## Goals / Non-Goals

**Goals:**
- Compute the `upload-staleness` judgment for the selected project from data already loaded by
  `DashboardService`, with no new entity fields, columns, or migrations.
- Keep the calculation a pure, independently unit-testable function over a list of timestamps —
  it should not need a repository or DB access itself.

**Non-Goals:**
- No caching or denormalized "is stale" field — the judgment is cheap enough (a sort and a median
  over one project's uploads) to compute on every dashboard load.
- No background job, scheduler, or outbound notification — see proposal.md.
- No per-project dismiss/mute state — see proposal.md.

## Decisions

**Median gap, not mean.** The "typical gap between uploads" is the median of consecutive gaps in
the combined, sorted upload timeline, not the mean. A burst of manual re-runs (several uploads
seconds apart) would drag a mean gap down and make the project look like it should upload far more
often than it really does, making the banner fire too eagerly after the burst. The median is
insensitive to that kind of outlier cluster.

**Combine case reports and flag-proof reports into one timeline.** Both are "an upload happened,"
and a project that alternates between the two shouldn't look artificially quiet in either the
case-report-only or flag-proof-only view. `upload-staleness`'s input is a plain list of
`DateTimeOffset`, not two separate lists, so the caller (`DashboardService`) does the merging.

**Compute on read, in `DashboardService`, not in a new service.** The same place that already
assembles `DashboardCaseReportView`/`DashboardFlagProofReportView` from the two report lists has
everything the calculation needs (`caseReports`, `flagProofReports`) with no extra query. A small
pure calculator (taking a sorted or unsorted list of timestamps plus "now") is added and called
inline; it does not need its own repository, service registration, or endpoint.

**5-upload minimum and 3x multiplier are fixed constants for this change, not configurable.**
Both are judgment calls (see proposal.md's open framing of them) rather than values derived from
data. Making them tunable (per-project or via config) is complexity with no current user demand;
they can be pulled into configuration later if real-world false positives/negatives show the fixed
values are wrong for some class of project.

## Risks / Trade-offs

- **A project with a highly irregular cadence (no real "typical" gap) may get spurious banners.**
  Median gap assumes some regularity; a project that uploads in unpredictable bursts (e.g. weekly
  for a month, then nothing, then daily) doesn't have a stable "typical" gap to measure against. →
  Accepted for this change: the 5-upload minimum and 3x tolerance already give meaningful slack,
  and no dismiss mechanism means a false-positive banner is transient (it disappears with the next
  upload), not a persistent nuisance.
- **The 3x multiplier is untuned.** It is a reasonable starting guess, not validated against real
  usage patterns. → Accepted as a v1 default; revisit once real dashboard usage shows whether it
  over- or under-fires.

## Migration Plan

None. No schema, entity, or stored-data changes — this is a pure read-time calculation over
existing timestamps, plus a new UI element. Ships and rolls back like any other stateless code
change.
