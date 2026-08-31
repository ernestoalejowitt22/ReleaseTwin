## 1. Repository reads

- [x] 1.1 `CaseReportRepository.ListByProjectInRangeAsync(projectId, from, to)` — `Query`
      with `SK BETWEEN CASEREPORT#<from> AND CASEREPORT#<to>`.
- [x] 1.2 Same for `FlagProofReportRepository` (`FLAGPROOF#` prefix).
- [x] 1.3 Tests: range boundaries inclusive/exclusive, empty range.

## 2. TrendService

- [x] 2.1 `Analytics/TrendService.cs` — `ForProject(projectId, window)` and
      `ForOrganization(orgId, window)`.
- [x] 2.2 Bucketing: daily (7d/30d) / weekly (90d), UTC boundaries, zero-fill gaps.
- [x] 2.3 Series: case pass rate, flag-proof pass rate (proven/eligible, exclude
      ineligible), run volume, classification breakdown. Null rate when denominator zero.
- [x] 2.4 Flakiest cases: group by CaseId, count pass↔fail flips over time, top 5.
- [x] 2.5 Org rollup: fan out over the org's projects, merge buckets.
- [x] 2.6 Tests: bucket assignment, weekly rollup, flip counting, null-rate, empty window,
      classification sums, org fan-out.

## 3. Endpoints

- [x] 3.1 `GET /projects/{id}/trends?window=` and `GET /trends?window=` — web-session auth,
      org-scoped, project ownership check.
- [x] 3.2 Entitlement gate on `trendAnalytics`; `EntitlementRequiredException` shape.
- [x] 3.3 `window` validation (only `7d`/`30d`/`90d`); default `30d`.
- [x] 3.4 Endpoint tests: auth, cross-org refusal, entitlement refusal, bad window.

## 4. Dashboard

- [x] 4.1 Trends tab on the project page + org dashboard.
- [x] 4.2 Dependency-free SVG line charts (rates, volume) + stacked bar (classification) +
      flakiest-cases list. (If a chart lib is added instead, call it out in the PR.)
- [x] 4.3 Window switcher (7 / 30 / 90).
- [x] 4.4 Upgrade prompt when the bootstrap entitlement set lacks `trendAnalytics`.

## 5. Validation

- [x] 5.1 `openspec validate trend-analytics --strict` passes.
- [x] 5.2 `dotnet build` + `dotnet test` green; report counts.
- [x] 5.3 `web/`: `npm run build` + `npx eslint` green.

## Decisions to lock (from proposal Open Questions)

- [x] D1 Fixed bucket granularity (daily ≤30d / weekly 90d). (proposed)
- [x] D2 Flakiest = pass↔fail flip count. (proposed)
- [x] D3 UTC day boundaries. (proposed)
- [x] D4 No GSI — native project-partition range query + in-memory org fan-out. (confirmed in design)
- [x] D5 Dependency-free SVG charts. (proposed)
