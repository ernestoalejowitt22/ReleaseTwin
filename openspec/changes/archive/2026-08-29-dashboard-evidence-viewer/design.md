## Context

See proposal.md — *Why*. The relevant current state:

- `OperationResult` (`src/ReleaseTwin.Core/Contracts.cs`) carries only `Succeeded`, `Detail`, `Captures`. No adapter emits structured request/response data.
- `CaseExecutor.ExecuteAsync` returns a single `CaseReport` (`src/ReleaseTwin.Core/CaseReport.cs`) — case id, oracle, fixture hash, pass/fail, classification, detail, cleanup status, duration. It discards per-step data.
- `FlagProof.cs` runs two executions and compares outcomes.
- The ingest contract (`hosted/…/Contracts/IngestRequests.cs`) is a deliberately-decoupled DTO; `IngestEndpoints` validates and stores via `CaseReportRepository` / `FlagProofReportRepository` into an in-repo store; `UsageCounterRepository` meters.
- `DashboardService` shapes everything the web BFF renders; retention/staleness math already lives in `UploadStalenessCalculator`.
- `project-secrets` and `hosted-journeys` are Paid-tier gated via `plan-tier-gating`; `adapter-credentials` shows the pattern for a CLI→hosted fetch of per-project config.
- There is no CI pipeline; a recurring purge/digest job pattern was just added by `operator-alerting`.

## Goals / Non-Goals

**Goals**

- Evidence off by default changes nothing — same bytes on the wire, same report, same tests green.
- Raw content is never transmitted: redaction is a mandatory CLI pass with no bypass path.
- Vendor-neutral core: the executor carries operation evidence opaquely.
- One coherent evidence document per run, rendered as a per-report drill-down.

**Non-Goals**

- Cross-run evidence diffing, evidence export/download, evidence search.
- Server-side redaction or PII detection — explicitly rejected (see Decisions).
- Making evidence capture the default, now or in this change.
- Streaming/live evidence; evidence is a post-run upload.

## Decisions

### D1: Redaction is CLI-only, server never inspects

The ingest API stores the evidence document as an opaque blob and never parses it for sensitive content. Rationale: the current guarantee ("only hashes leave your infra") is *structural*; the weakest acceptable replacement is "redaction provably runs on your machine, in code you can read, before the socket opens." A server-side scrubber would mean raw PII transits and is transiently held — strictly worse. Trade-off: a customer misconfiguring their denylist can upload PII; mitigated by fail-closed redaction, conservative built-ins, a short default retention, and the dashboard stating provenance.

Alternative considered: server-side redaction with client hints — rejected, moves the trust boundary the wrong way.

### D2: Evidence is a separate optional return value, not fields on `CaseReport`

`CaseExecutor.ExecuteAsync` gains an overload/option (`ExecutionOptions { CaptureEvidence: bool }`) and, when set, returns `CaseExecutionResult { CaseReport Report; RunEvidence? Evidence }`. `CaseReport` is untouched — keeps D1 of the original hosted design (stable report contract) intact and keeps the "disabled = identical" guarantee trivial to verify.

`RunEvidence`: `CaseId`, `OracleLocator`, `IReadOnlyList<StepEvidence> Steps`, optional `Leg` label (for flag-proof). `StepEvidence`: `OperationName`, `Outcome` enum, `Duration`, `AssertionDetail?` (`Expression`, `Expected`, `Observed`), `object? OperationEvidence` (opaque). Flag-proof returns `FlagProofExecutionResult` with a `RunEvidence` per leg.

### D3: Operations emit evidence through an opt-in interface, not a changed `IOperation`

Add `IEvidenceEmittingOperation { object? DrainEvidence(); }` (or an `out object? evidence` on a new result shape). Executor checks `operation is IEvidenceEmittingOperation` only when capture is on. Existing operations and the toy adapters need zero changes. HTTP adapter emits `{ request: {method,url,headers,body}, response: {status,headers,body}, timing }`; assertion ops emit the compared path/expected/actual; UI adapter emits an action log + screenshot handles (bytes carried out-of-band to the CLI, not through core).

Alternative: bake `Evidence` onto `OperationResult` as nullable — simpler but forces every adapter author to think about it and pollutes the common path. Opt-in interface keeps evidence a bolt-on.

### D4: Redaction model — hybrid, ordered, fail-closed

CLI redaction pass over the assembled `RunEvidence`:

1. **Built-in denylist** (always): drop `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie` headers; drop object keys matching `/(password|secret|token|api[_-]?key|authorization|credential)/i`; replace any substring equal to a value the run resolved from env/`project-secrets` with `«redacted»`.
2. **Per-case denylist**: `evidence.redact: [ {header|jsonPath|field|selector|region} ]` in the case file — masks additional locations.
3. **Per-case allowlist**: `evidence.capture: [ jsonPath|field ]` — re-includes something a built-in *key-name* rule dropped (never overrides rule 1's header or resolved-secret masks).

Any rule that throws on a given payload ⇒ that whole field (body/header set/screenshot) is dropped. Redaction lives in a dedicated `EvidenceRedactor` in the CLI project with heavy unit coverage; it is the only thing between `RunEvidence` and the upload client.

Case-file schema gains an `evidence:` block ⇒ **`case-loading` needs a delta** (added to tasks). Journeys authored in the builder get the same block ⇒ `hosted-journeys` / web builder follow-up, flagged as an open question for scope.

### D5: Wire shape — one optional `evidence` object on the existing ingest requests

`IngestCaseReportRequest` / `IngestFlagProofReportRequest` gain `EvidenceDocument? Evidence`. `EvidenceDocument` is a JSON tree with a declared max serialized size (proposal: 256 KB text; screenshots uploaded as separate multipart blobs with their own count/size caps, referenced by id from the document). Oversize ⇒ 413, nothing stored (atomic, matches existing "malformed rejected atomically"). Free-tier ⇒ report stored, evidence dropped, response body carries `evidenceAccepted: false` ⇒ CLI warning.

### D6: Storage & retention

New `UploadedRunEvidence` entity (`Id`, `ProjectId`, `ReportId` + report kind, `UploadedAt`, `DocumentJson`, `ScreenshotBlobIds`) + `IRunEvidenceRepository`. Screenshots to a blob store abstraction (filesystem impl now, matching the current in-repo store's ethos). `Project` entity gains `EvidenceRetentionDays` (nullable ⇒ system default 30; system max 365). New `EvidenceCaptureDefault bool` on project for the CLI fetch.

Purge: extend the `operator-alerting` scheduled job host with an `EvidencePurgeJob` — deletes `UploadedRunEvidence` where `UploadedAt + project.RetentionDays < now`, plus orphaned blobs; never touches report rows. Dashboard marks such reports "evidence expired".

### D7: CLI fetch of the per-project capture default

Reuse the `adapter-credentials` fetch pattern: when a token is set, `GET /api/projects/{id}/evidence-config` → `{ captureDefault, retentionDays }`. Env var `RELEASETWIN_EVIDENCE=on|off` overrides. No token ⇒ env var only ⇒ capture may still happen for local display but no upload.

## Risks / Trade-offs

- **Customer misconfiguration uploads PII** → fail-closed redaction; conservative always-on built-ins; 30-day default retention; provenance shown in UI; docs explicitly frame the opt-in as "you own the redaction rules."
- **Screenshot redaction is weak** (region masks only, no OCR) → labelled best-effort everywhere; region/selector masks applied pre-upload; customers can disable screenshot evidence per case.
- **Evidence document size blows up** (large response bodies) → per-field body truncation in the adapter emitter (configurable cap, default 32 KB/body) before it ever reaches redaction; hard 256 KB document cap at ingest.
- **Every adapter now has an evidence code path** → opt-in interface means adapters adopt incrementally; HTTP + assertions first, UI second, Azure DevOps / LaunchDarkly last; toy adapters never.
- **The guarantee downgrade is a marketing/trust liability** → default-off preserves the strong claim for anyone who wants it; `installation-model.md` and `customer-pilot-guide.md` updates are in tasks, not optional.
- **Core contract churn** (`ExecuteAsync` signature) → additive overload, old signature kept delegating with capture off.

## Migration Plan

1. Ship core + adapter-sdk evidence seam (no behavior change, capture path dormant).
2. Ship HTTP/assertion evidence emitters + `EvidenceRedactor` + CLI opt-in (still off by default; usable by env var).
3. Ship ingest `evidence` field + `evidence-store` + retention config + purge job.
4. Ship dashboard detail view + project settings.
5. Ship UI-adapter and remaining adapter emitters.
6. Update `installation-model.md`, `customer-pilot-guide.md`, README privacy wording.

Rollback: feature is inert with capture off; disabling the hosted `evidence-config` endpoint and ignoring the ingest `evidence` field reverts observable behavior without a data migration. Stored evidence can be bulk-purged by setting retention to 0.

## Open Questions

- Screenshot blob storage backend for production (filesystem vs object store) — deferrable; the `IEvidenceBlobStore` abstraction isolates it. Shipped with a filesystem implementation.

## Resolved

- **Journey-builder evidence rules**: the web journey builder now emits an `evidence:` block (allowlist textarea + denylist rows) into the YAML it saves — so a hosted journey carries the same redaction rules a hand-written case file would, parsed by the same `CaseFileLoader.ParseYaml`. No `hosted-journeys` capability change was needed (journeys already store opaque YAML); the `dashboard` "visually author a journey" requirement gains a scenario.
