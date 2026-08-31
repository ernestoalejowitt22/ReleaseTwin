## 1. Case model + loader

- [x] 1.1 `ReleaseTwin.Core` case model — optional `Release` string; no execution effect.
- [x] 1.2 Case loader parses `release:`; absent → null.
- [x] 1.3 Core/loader tests: present, absent, non-string rejected with a clear message.

## 2. CLI upload

- [x] 2.1 Include `release` in the uploaded case-report and flag-proof-report DTOs.
- [x] 2.2 CLI test: a labelled case's upload payload carries `release`.

## 3. Ingest + storage

- [x] 3.1 `release` on `UploadedCaseReport` / `UploadedFlagProofReport` + item attribute
      (`CaseReportRepository` / `FlagProofReportRepository` ToItem/ToReport).
- [x] 3.2 Ingest endpoint accepts optional `release`; no-`release` payload unchanged.
- [x] 3.3 Tests: ingest with/without `release`; round-trips through storage.

## 4. Rollup service + endpoints

- [x] 4.1 `Releases/ReleaseRollupService.cs` — `ListReleases(projectId)`,
      `Rollup(projectId, label, window=14d)`.
- [x] 4.2 Latest-result-per-case across case + flag-proof reports; green / failing / stale
      classification; headline state (Proven / Not proven / Incomplete).
- [x] 4.3 `GET /projects/{id}/releases`, `GET /projects/{id}/releases/{label}?window=` —
      web-session auth, org scope, project ownership, `releaseRollup` entitlement gate.
- [x] 4.4 Tests: latest wins, failing → Not proven, stale → Incomplete, all-green →
      Proven, empty releases list, entitlement refusal, cross-org refusal.

## 5. Dashboard

- [x] 5.1 Releases section on the project page — label list + rollup (headline badge,
      counts, per-case table).
- [x] 5.2 Recency-window control; upgrade prompt when `releaseRollup` absent from the
      bootstrap entitlement set.

## 6. Docs + examples

- [x] 6.1 Add `release:` to an `examples/cases/*.yaml` and the case-file docs.
- [x] 6.2 `docs/quickstart.md` + `README.md` case snippet show the field.

## 7. Validation

- [x] 7.1 `openspec validate release-readiness-rollup --strict` passes.
- [x] 7.2 `dotnet build` + `dotnet test` green; report counts.
- [x] 7.3 `web/`: `npm run build` + `npx eslint` green.

## Decisions to lock (from proposal Open Questions)

- [x] D1 `release` is a case-file field only — no dashboard-side tagging in v1. (confirmed)
- [x] D2 Single release string per case (not a list). (proposed)
- [x] D3 Zero-recent-runs release → Incomplete. (proposed)
- [x] D4 Latest result = most recent case-or-flag-proof report; proven/passed = green,
      ineligible = stale. (proposed)
