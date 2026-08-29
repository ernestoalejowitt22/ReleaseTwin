## Why

Today a customer can see *that* a case passed or failed on the dashboard, but never *what it saw* — no request/response, no assertion detail, no per-step trace. The full evidence exists only as transient CLI stdout in their own infra. The `installation-model` and `ingest-api` guarantees deliberately keep it that way: the ingest contract is *structurally* incapable of carrying fixture content or response bodies. That guarantee is a genuine differentiator, but it also means the dashboard can't answer the first question a user asks about a red run: "why?"

This change adds an **opt-in** path for evidence to reach the dashboard, with redaction of PII/secrets performed **entirely in the customer's own CLI before upload** — so raw content still never leaves their infra, and the default (evidence off) preserves the current metadata-only guarantee unchanged.

## What Changes

- **Operations emit structured evidence.** A new optional seam in the adapter contract lets an operation attach structured evidence to its result (e.g. HTTP: method/URL/status/headers/body; UI: action log + screenshot references). Adapters that don't implement it are unaffected.
- **The executor aggregates per-step evidence** into an optional `RunEvidence` alongside the existing `CaseReport` — ordered steps, each with its outcome, duration, and adapter-emitted evidence, plus assertion detail (path / expected / actual). Nothing is aggregated unless evidence capture is enabled for the run.
- **New capability `evidence-capture`**: defines what evidence a run may collect, and the **hybrid redaction model** applied in the CLI before any upload — built-in stripping of known secret-bearing headers/fields by default, plus per-case allowlist (capture more) and denylist (redact more) overrides via JSONPath/field rules. Redaction failure (a rule that can't be evaluated) fails closed: that evidence field is dropped, not uploaded raw.
- **`cli-runner` gains an opt-in evidence switch** (env var + per-project default), off by default. When on and an API token is configured, the CLI captures, redacts, and uploads run evidence after execution. Upload failure is a warning, never a change to the case outcome or exit code — same rule as report upload today.
- **`ingest-api` gains an optional evidence payload.** **BREAKING (contract-guarantee change):** the requirement "The ingest contract has no field for sensitive content" is amended — the contract MAY carry a redacted evidence document, but still defines no field for credentials/tokens, enforces a size cap, and treats redaction as the caller's completed responsibility. The default payload shape (no evidence) is unchanged.
- **New capability `evidence-store`**: hosted storage of uploaded evidence, org-scoped exactly as reports are, with **per-project configurable retention** (a project-set window, capped at a maximum) and a purge that deletes evidence past its window while leaving the metadata report intact.
- **`dashboard` gains a per-report evidence detail view**: drill into any case or flag-proof report to see the redacted step-by-step evidence, with a visible "evidence redacted in your CLI before upload" affordance and the retention window in effect. Adds a project setting to enable evidence capture and set the retention window.

### Assumptions

- Evidence upload + hosted storage is a **Paid-tier** feature, consistent with `project-secrets` and `hosted-journeys`. CLI capture + redaction + local display is not gated; only the upload/store path checks entitlement.
- UI-adapter screenshots are stored as redacted image blobs under the same retention window; redaction of on-screen PII is best-effort (the hybrid model's denylist can mask regions/selectors), and the detail view labels screenshots as best-effort-redacted.
- First version does not diff evidence across runs or export it; it renders one run's evidence.

## Capabilities

### New Capabilities
- `evidence-capture`: what structured evidence a run may collect from any adapter, and the hybrid (built-in + per-case allowlist/denylist) redaction applied in the CLI before upload, failing closed on any un-evaluable rule.
- `evidence-store`: hosted, org-scoped storage of uploaded run evidence with per-project configurable retention (capped) and a purge that preserves the underlying metadata report.

### Modified Capabilities
- `core-execution`: the executor SHALL, when evidence capture is enabled for a run, aggregate per-step outcome/duration/adapter-emitted evidence and assertion detail into an optional run-evidence document carried alongside the report; with capture disabled, execution and the report are byte-for-byte unchanged.
- `adapter-sdk`: an operation MAY attach structured evidence to its `OperationResult` through a new optional contract member; adapters that do not are unaffected and the core still references no adapter-specific evidence shape.
- `cli-runner`: opt-in evidence capture (off by default); when enabled with a token configured, the CLI captures, redacts (hybrid model), and uploads run evidence; upload failure is a warning that does not alter the case outcome or exit code.
- `ingest-api`: the contract MAY accept an optional redacted evidence document (size-capped, still no credential field); redaction is the caller's completed responsibility; the no-evidence payload shape is unchanged.
- `dashboard`: a per-report evidence detail view rendering the redacted step-by-step evidence for Paid-tier projects that have enabled capture, plus a project setting for enabling capture and choosing the retention window; evidence is shown only within the uploading organization.

## Impact

- **Core**: `OperationResult` (new optional evidence member), `CaseExecutor` (per-step evidence aggregation, gated), new `RunEvidence` type, `FlagProof` (evidence for each leg).
- **Adapters**: `ReleaseTwin.Adapters.Http`, `ReleaseTwin.Adapters.Ui`, Azure DevOps, LaunchDarkly — each optionally emits evidence; toy adapters left as-is.
- **CLI**: `CliRunner` — evidence flag resolution, redaction pass, evidence upload call; new redaction-rule parsing in case loading (`case-loading` may need a delta if rules live in the case file schema — to be confirmed in design).
- **Hosted API**: new `UploadedRunEvidence` entity + repository + blob storage for screenshots, ingest endpoint extension, retention config on the project entity, a purge job (extends the existing operator-side scheduled work), `DashboardService` + endpoints for the detail view and settings.
- **Web**: new evidence detail route/view, project settings controls, BFF endpoints and types.
- **Specs**: 2 new, 5 modified (listed above).
- **Docs**: `installation-model.md` and `customer-pilot-guide.md` must be updated — the "only hashes and pass/fail ever leave your infra" claim becomes "…unless you opt into evidence upload, which redacts before sending."
