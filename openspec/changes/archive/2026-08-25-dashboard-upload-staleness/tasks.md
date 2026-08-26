## 1. Staleness calculator

- [x] 1.1 Implement a pure calculator that takes a list of upload timestamps and "now", and
      returns whether the project is stale, per the `upload-staleness` spec (5-upload minimum,
      median-of-gaps typical cadence, 3x multiplier).
- [x] 1.2 Unit test: fewer than 5 uploads never judged stale, regardless of last-upload age.
- [x] 1.3 Unit test: gap at or below 3x median gap is not stale; gap above it is stale.
- [x] 1.4 Unit test: a burst of near-simultaneous uploads doesn't drag the median down enough to
      cause a false stale judgment on a project with an otherwise steady cadence.
- [x] 1.5 Unit test: an infrequent but steady (e.g. ~30-day) cadence project is not judged stale
      until its gap exceeds ~3x that cadence.

## 2. Dashboard integration

- [x] 2.1 In `DashboardService.GetDashboardViewAsync`, merge `caseReports` and `flagProofReports`
      timestamps for the selected project into one sorted timeline and run the staleness
      calculator against it.
- [x] 2.2 Add the staleness judgment to `DashboardView` (or the selected-project summary) so the
      web UI can render it.
- [x] 2.3 Update/add tests around `DashboardService` covering: a stale project's view carries the
      flag; a non-stale or too-new project's view does not.

## 3. Banner UI

- [x] 3.1 Add a banner to the project view in `web/src/app/dashboard` that renders when the
      selected project is judged stale, shown alongside (not replacing) run history.
- [x] 3.2 Verify in the browser: a project with enough seeded history and an old last-upload shows
      the banner; a fresh or actively-uploading project does not.
