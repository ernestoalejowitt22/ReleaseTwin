## 1. Core evidence model (dormant, no behavior change)

- [x] 1.1 Add `RunEvidence`, `StepEvidence`, `StepEvidenceOutcome`, `AssertionDetail` types to `ReleaseTwin.Core` (vendor-neutral; `OperationEvidence` is `object?`)
- [x] 1.2 Add `IEvidenceEmittingOperation` (or equivalent drain hook) to `ReleaseTwin.AdapterSdk` / core contracts — optional, existing `IOperation` unchanged
- [x] 1.3 Add `ExecutionOptions { bool CaptureEvidence }` and a `CaseExecutionResult { CaseReport Report; RunEvidence? Evidence }`; add `CaseExecutor.ExecuteAsync` overload taking options; keep the old signature delegating with capture off
- [x] 1.4 In `RunPipelineAsync`, when capture is on, record per-step name/outcome/duration/assertion-detail and drain operation evidence into an ordered `RunEvidence`; mark post-halt steps not-executed
- [x] 1.5 Extend `FlagProof` to return a `RunEvidence` per leg (labelled known-bad / known-good) when capture is on
- [x] 1.6 Tests: capture-off produces identical `CaseReport` and no evidence; capture-on produces ordered records; flag-proof produces two labelled legs; core references no adapter-specific evidence field

## 2. Adapter evidence emitters

- [x] 2.1 `ReleaseTwin.Adapters.Http` — `http.request` emits `{request{method,url,headers,body}, response{status,headers,body}, timing}` with per-body truncation cap (default 32 KB)
- [x] 2.2 `ReleaseTwin.Adapters.Http` — `http.assertJsonPath` emits `{path, expected, observed}`
- [x] 2.3 `ReleaseTwin.Adapters.Ui` — emit an ordered action log plus screenshot handles (bytes returned to the CLI out-of-band, not through core)
- [x] 2.4 `ReleaseTwin.Adapters.AzureDevOps` and `ReleaseTwin.Adapters.LaunchDarkly` — emit operation-appropriate evidence (request/response summaries, feature-state transitions)
- [x] 2.5 Confirm `ToyHttp` / `ToyFile` emit nothing and still pass unchanged
- [x] 2.6 Tests per adapter: evidence present when drained, `null` otherwise, bodies truncated at the cap

## 3. Case-file evidence block (`case-loading` delta)

- [x] 3.1 Parse an `evidence:` block: `capture: [jsonPath|field]` allowlist and `redact: [{header|jsonPath|field|selector|region}]` denylist; absent block = no rules
- [x] 3.2 Validation errors for malformed rules are clear load-time errors, consistent with existing case-loading error style
- [x] 3.3 Tests: block parses; absent block is inert; malformed rule rejected at load

## 4. CLI redaction + opt-in + upload (`cli-runner` delta)

- [x] 4.1 `EvidenceRedactor` in the CLI project: ordered built-in denylist → per-case denylist → per-case allowlist; fail-closed (drop the whole field on any un-evaluable rule); mask resolved secret/token values wherever they appear
- [x] 4.2 Exhaustive `EvidenceRedactor` unit tests: auth/cookie headers stripped, credential-shaped keys stripped, resolved-secret substring masked in bodies, denylist JSONPath/field/selector/region masks, allowlist re-includes a key-name-dropped field but NOT an auth header or secret, unparseable body dropped, screenshot region masked
- [x] 4.3 Resolve capture opt-in: `RELEASETWIN_EVIDENCE=on|off` env var overrides hosted per-project default; default off; env precedence tests
- [x] 4.4 When token configured: `GET /api/projects/{id}/evidence-config` for `{captureDefault, retentionDays}` (reuse `adapter-credentials` fetch pattern)
- [x] 4.5 When capture on + token set: run executor with `CaptureEvidence`, redact, upload redacted document (+ screenshot blobs) alongside the report/flag-proof upload
- [x] 4.6 Evidence upload failure / not-accepted → distinct warning; case outcome, report upload, and exit code unaffected
- [x] 4.7 Tests: no opt-in = no capture/upload and byte-identical behavior; capture without token = no upload; rejected evidence = warning only

## 5. Ingest contract (`ingest-api` delta)

- [x] 5.1 Add optional `EvidenceDocument? Evidence` to `IngestCaseReportRequest` and `IngestFlagProofReportRequest`; define `EvidenceDocument` shape; no credential field anywhere
- [x] 5.2 Enforce max serialized evidence size (256 KB) + screenshot count/size caps; oversize → reject entire request atomically, store nothing
- [x] 5.3 Multipart handling for screenshot blobs referenced by id from the document
- [x] 5.4 Tests: metadata-only payload byte-identical to pre-change; oversize rejected atomically; schema exposes no credential field

## 6. Evidence store + retention + purge (`evidence-store` delta)

- [x] 6.1 `UploadedRunEvidence` entity + `IRunEvidenceRepository` + in-repo store impl; blob-store abstraction (filesystem impl) for screenshots
- [x] 6.2 Add `EvidenceRetentionDays` (nullable → default 30, max 365) and `EvidenceCaptureDefault` to the `Project` entity
- [x] 6.3 Ingest: store evidence only for Paid-tier orgs; Free-tier → store report, drop evidence, respond `evidenceAccepted: false`
- [x] 6.4 `GET /api/projects/{id}/evidence-config` and `PUT` to set `captureDefault` + `retentionDays` (reject > max); org-scoped auth
- [x] 6.5 `EvidencePurgeJob` in the `operator-alerting` scheduled-job host: delete evidence past `UploadedAt + retentionDays` and orphaned blobs; never touch report rows
- [x] 6.6 Tests: cross-org read denied; evidence links to exactly one report; Free-tier drop; retention cap enforced; purge removes expired evidence and preserves the report; lowering the window makes old evidence eligible

## 7. Dashboard evidence view + settings (`dashboard` delta)

- [x] 7.1 `DashboardService` + endpoint: fetch redacted evidence for a given report (org-scoped); expose per-report `hasEvidence` / `evidenceStatus` (none / available / expired / not-entitled)
- [x] 7.2 Web: per-report evidence detail route/view — ordered steps with outcome/duration/adapter evidence; assertion path/expected/observed; flag-proof legs as distinct sections
- [x] 7.3 Web: "redacted in your CLI before upload" provenance line; screenshots labelled best-effort-redacted
- [x] 7.4 Web: per-project evidence settings (Paid only) — enable capture default, set retention window ≤ max, show effective window; Free-tier shows unavailable + tier reason
- [x] 7.5 BFF endpoints + `web/src/lib/types.ts` additions; reports without evidence show run history unchanged with a reason
- [~] 7.6 Tests: backend coverage via `DashboardServiceTests` + `EvidenceIngestApiTests` + `EvidenceStoreTests` (cross-org, Free-tier drop, retention cap, purge). No new Cypress spec added — matches `hosted-react-frontend`'s stance of not adding speculative browser tests; `web build` + `tsc` + `eslint` green.

## 8. Docs

- [x] 8.1 Update `docs/installation-model.md` — the "only hashes / pass-fail leave your infra" claim gets the opt-in evidence carve-out
- [x] 8.2 Update `docs/customer-pilot-guide.md` — evidence-in-dashboard as a pilot talking point, framed as customer-owned redaction
- [x] 8.3 Update `README.md` hosted-platform paragraph to mention opt-in evidence + CLI-side redaction

## 9. Validation

- [x] 9.1 `openspec validate dashboard-evidence-viewer --strict` passes
- [~] 9.2 Full solution build + all .NET test projects green (253 tests); `web build` + `tsc` + `eslint` green; CLI example run with evidence off is unchanged (`2 passed, 2 failed`). Manual `RELEASETWIN_EVIDENCE=on` end-to-end against a live hosted instance not run (no local hosted deployment); exercised instead via `CliRunnerEvidenceTests` (redacted `evidence` in the upload body) + `EvidenceIngestApiTests` (stored server-side).

## 10. Journey-builder evidence rules (`dashboard` delta — resolved open question)

- [x] 10.1 `web/src/app/journeys/[journeyId]/journey-builder.tsx` — allowlist textarea + denylist rows (kind + value) emitting an `evidence:` block into the generated/saved YAML
- [x] 10.2 CLI side already covered: `CaseFileLoader.ParseYaml` (used for hosted journeys) parses the `evidence:` block — see task 3
- [x] 10.3 `dashboard` spec gains the "journey builder can author evidence redaction rules" requirement
