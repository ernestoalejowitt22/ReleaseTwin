## Context

See proposal.md — Why. Constraints that shape the approach:

- ReleaseTwin's core promise is a **deterministic** verdict and evidence hashes. Execution happens
  in the customer's infra; only redacted metadata and (Paid tier) redacted evidence are uploaded.
  Any probabilistic component must be strictly advisory and off the correctness path.
- `evidence-capture` already redacts screenshots inside the CLI before upload, and
  `evidence-store` already gates evidence on the Paid tier with a per-project retention/purge.
  This change reuses both rather than adding parallel machinery.
- The `ui-adapter` today only has "assert visible" — there is no baseline-screenshot assertion
  yet, so it must be added for this feature to have an input.
- The hosted API is a JSON-only .NET Lambda with no GPU today. A vision model is a genuinely new
  dependency.

## Goals / Non-Goals

**Goals:**
- Add a deterministic `ui.assertScreenshot` baseline assertion whose pixel diff is the verdict.
- Add an advisory `visual-analysis` result with a fixed, location-neutral schema.
- Keep the analyzer swappable: hosted service now, customer-hosted model later, same schema.
- Reuse existing redaction, Paid-tier gating, retention, and per-tenant scoping.

**Non-Goals:**
- Fine-tuning or training any model. Prompt-only, off-the-shelf open-weights VLM.
- Making analysis authoritative, or letting it gate CI / change an exit code.
- General "chat with your test run" features.
- Automatic video analysis. Video is stored-if-opted-in and analyzed only on explicit request.
- A customer-hosted analyzer implementation in this change — only the schema contract that keeps
  that path open.

## Decisions

### D1: Pixel diff is the verdict; analysis is a sidecar record
`ui.assertScreenshot` computes a deterministic pixel diff (normalized compare with a declared
per-step threshold, anti-alias-tolerant) and that alone sets pass/fail. The analysis result is
attached to the step's evidence entry as a separate field, never consulted by core execution,
classification, or flag-proof adjudication.
- *Alternative rejected:* let analysis suppress a failure it deems noise. Breaks the determinism
  guarantee and makes CI outcomes model-dependent.

### D2: Analysis runs post-assertion, async, best-effort
When enabled, the CLI (or hosted service) submits the redacted triplet after the step resolves.
The run does not block on it beyond a short time budget; on timeout/error the result is
`analysis-unavailable`. For the retry-hint case, a bounded synchronous wait (within the step's
existing timeout) is allowed so a `rendering-noise` verdict can inform a retry already permitted
by the case policy — never adding retries.
- *Alternative rejected:* synchronous mandatory analysis. Couples run latency and reliability to a
  GPU service.

### D3: Hosted analyzer first, behind an OpenAI-compatible boundary
A new hosted endpoint accepts `{orgId, reportRef, assertionRef, baseline, actual, diff}` (images
already redacted by the CLI) and returns the fixed schema. Behind it, an open-weights VLM
(Qwen2.5-VL-7B class, 4-bit) served by vLLM with guided/JSON decoding on a single GPU
(serverless GPU acceptable given bursty load). Prompt + few-shot examples are versioned as an
asset; `{modelId, promptVersion}` is stamped on every result.
- *Alternative rejected:* a hosted proprietary vision API. Cost/lock-in, and it forecloses the
  customer-hosted story that is the eventual enterprise pitch.
- *Alternative rejected:* ship the model in the CLI now. Large artifact, GPU expectation on the
  customer, slower iteration. The schema is location-neutral so this stays possible later.

### D4: Fixed classification set `{rendering-noise, real-regression, inconclusive}` + `analysis-unavailable`
Small closed set keeps the model's job well-defined and the dashboard rendering simple.
`inconclusive` is a real model output (low confidence / genuinely ambiguous);
`analysis-unavailable` is an operational state, not a model output.
- *Alternative rejected:* free-form labels or a larger taxonomy (font/layout/timing/…). Harder to
  evaluate, more prompt drift, little added user value over the prose explanation.

### D5: Reuse Paid-tier gate and per-project retention
Hosted analysis acceptance checks the same Paid-tier entitlement `evidence-store` uses. Analysis
results and any stored recording inherit the report's organization scope and the project's
retention window and purge. No new gating or lifecycle system.

### D6: Video is deliberately second-class
Recordings are marked not-proof and not-redaction-guaranteed at capture, excluded from every
evidence hash and adjudication path, stored (Paid tier) under the normal retention window, and
analyzed only via an authenticated on-demand endpoint that samples frames (≈1 fps, downscaled)
with a hard duration cap.
- *Alternative rejected:* treat video as first-class evidence. Not byte-deterministic across
  codecs/hardware, so it cannot be hashed as proof; large storage cost; analysis cost 10–50× a
  screenshot.

### D7: Evaluation before rollout
A held-out labelled set (100–300 real triplets from design-partner UI suites) scored for
classification accuracy / per-class precision, plus an LLM-judge rubric for the prose. This gates
enabling the feature for anyone and is re-run on any prompt or model bump (the version stamp
makes results attributable).

## Risks / Trade-offs

- **Model says "noise" on a real regression that then ships** → analysis never suppresses the
  failure; the verdict already failed and CI already blocked. Worst case is a misleading
  explanation, shown as advisory. Track false-noise rate in the eval set.
- **Screenshots leak PII the CLI redaction missed** → redaction is best-effort and labelled today;
  this change adds no new exposure vs. existing screenshot upload, keeps analysis on the
  post-redaction images only, and keeps the whole feature Paid-tier + opt-in. Document clearly.
- **GPU cost / availability** → serverless GPU or a single small instance; screenshot analysis is
  fractions of a cent batched; graceful degradation means an outage is invisible to runs.
- **Per-tenant batching bug mixes orgs in one prompt** → hard rule: one org per analyzer request;
  covered by a spec scenario and an isolation test.
- **Prompt drift changes verdicts silently between subscription periods** → every result carries
  `{modelId, promptVersion}`; changes are versioned assets and re-evaluated; dashboard shows the
  stamp.
- **Baseline management burden** (stale baselines, per-environment rendering) → out of scope here
  beyond "missing baseline is a clear failure"; baseline update workflow can be a later change.

## Migration Plan

1. Ship `ui.assertScreenshot` + triplet emission + CLI opt-in flags. No hosted dependency yet;
   analysis result simply always `analysis-unavailable` until the service exists.
2. Stand up the hosted analyzer (vLLM + VLM, endpoint, per-tenant isolation, Paid-tier gate,
   result storage) behind a feature flag; run the eval set.
3. Enable for design partners; monitor false-noise rate and prose quality.
4. Add video capture (opt-in), storage markers, and the on-demand video-analysis endpoint.
5. Dashboard rendering of the advisory result ships alongside step 2.

Rollback: disable the feature flag — `ui.assertScreenshot` keeps working as a pure pixel-diff
assertion, results revert to `analysis-unavailable`, nothing else changes.

## Open Questions

- Exact pixel-diff library/threshold defaults for `ui.assertScreenshot` (implementation detail,
  does not affect the spec or task breakdown).
- Whether the retry-hint synchronous wait (D2) is worth the coupling, or the first release should
  be async-only with no retry influence. Can be decided during step 1 without spec changes.
- Frame-sampling rate and duration cap constants for on-demand video analysis.
