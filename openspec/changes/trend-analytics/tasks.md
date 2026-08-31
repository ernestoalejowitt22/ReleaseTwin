## 1. Repository reads

- [ ] 1.1 `CaseReportRepository.ListByProjectInRangeAsync(projectId, from, to)` — `Query`
      with `SK BETWEEN CASEREPORT#<from> AND CASEREPORT#<to>`.
- [ ] 1.2 Same for `FlagProofReportRepository` (`FLAGPROOF#` prefix).
- [ ] 1.3 Tests: range boundaries inclusive/exclusive, empty range.

## 2. TrendService

- [ ] 2.1 `Analytics/TrendService.cs` — `ForProject(projectId, window)` and
      `ForOrganization(orgId, window)`.
- [ ] 2.2 Bucketing: daily (7d/30d) / weekly (90d), UTC boundaries, zero-fill gaps.
- [ ] 2.3 Series: case pass rate, flag-proof pass rate (proven/eligible, exclude
      ineligible), run volume, classification breakdown. Null rate when denominator zero.
- [ ] 2.4 Flakiest cases: group by CaseId, count pass↔fail flips over time, top 5.
- [ ] 2.5 Org rollup: fan out over the org's projects, merge buckets.
- [ ] 2.6 Tests: bucket assignment, weekly rollup, flip counting, null-rate, empty window,
      classification sums, org fan-out.

## 3. Endpoints

- [ ] 3.1 `GET /projects/{id}/trends?window=` and `GET /trends?window=` — web-session auth,
      org-scoped, project ownership check.
- [ ] 3.2 Entitlement gate on `trendAnalytics`; `EntitlementRequiredException` shape.
- [ ] 3.3 `window` validation (only `7d`/`30d`/`90d`); default `30d`.
- [ ] 3.4 Endpoint tests: auth, cross-org refusal, entitlement refusal, bad window.

## 4. Dashboard

- [ ] 4.1 Trends tab on the project page + org dashboard.
- [ ] 4.2 Dependency-free SVG line charts (rates, volume) + stacked bar (classification) +
      flakiest-cases list. (If a chart lib is added instead, call it out in the PR.)
- [ ] 4.3 Window switcher (7 / 30 / 90).
- [ ] 4.4 Upgrade prompt when the bootstrap entitlement set lacks `trendAnalytics`.

## 5. Validation

- [ ] 5.1 `openspec validate trend-analytics --strict` passes.
- [ ] 5.2 `dotnet build` + `dotnet test` green; report counts.
- [ ] 5.3 `web/`: `npm run build` + `npx eslint` green.

## Decisions to lock (from proposal Open Questions)

- [ ] D1 Fixed bucket granularity (daily ≤30d / weekly 90d). (proposed)
- [ ] D2 Flakiest = pass↔fail flip count. (proposed)
- [ ] D3 UTC day boundaries. (proposed)
- [ ] D4 No GSI — native project-partition range query + in-memory org fan-out. (confirmed in design)
- [ ] D5 Dependency-free SVG charts. (proposed)
