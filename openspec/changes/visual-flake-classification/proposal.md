## Why

UI tests are the flakiest tests a team owns, and the dominant cause is visual assertions failing
on rendering noise — font hinting, anti-aliasing, sub-pixel layout, animation timing — not real
regressions. A deterministic pixel diff can prove *that* pixels changed; it cannot tell a human
whether "the submit button disappeared" or "Chrome changed its text rendering." Today that
triage is entirely manual, and it is the single biggest source of wasted time in a UI suite.

ReleaseTwin already has the right raw material: the `ui-adapter` drives a real browser, and
`evidence-capture` already handles best-effort screenshot redaction inside the customer's CLI.
What is missing is (1) a first-class visual assertion that emits a baseline/actual/diff triplet,
and (2) an advisory layer that classifies a failed visual assertion as rendering-noise vs. real
regression and explains it in prose — without ever touching the deterministic pass/fail verdict
that is the product's whole reason to exist.

## What Changes

- **New `ui.assertScreenshot` step** in the UI adapter: compares the current render against a
  stored baseline; a pixel difference beyond a declared threshold is a deterministic failure,
  classified and cleaned up like any other step. On failure it emits the baseline image, the
  actual image, and a diff image as step evidence (the "triplet").
- **New `visual-analysis` capability**: an opt-in, advisory vision-model layer that takes the
  redacted triplet from a failed visual assertion and returns a fixed-schema result —
  classification (`rendering-noise` | `real-regression` | `inconclusive`), a confidence, a short
  prose explanation, and a stamp of the model identifier + prompt version that produced it.
  - The pixel diff remains the **authoritative** verdict. Analysis never changes a run's
    pass/fail, its report outcome, or a flag-proof adjudication.
  - A `rendering-noise` classification MAY inform an auto-retry decision, but only within the
    case's existing retry-policy limits — it can never extend retries.
  - The output schema is **identical regardless of where the model runs** (hosted service now;
    a customer-hosted open-weights VLM later), so downstream consumers never branch on location.
  - Graceful degradation: analysis unavailable, errored, or timed out leaves the run, report,
    and upload byte-for-byte unchanged; the result is recorded as `analysis-unavailable`.
  - Delivered hosted-first and opt-in, gated to the Paid tier (same gate `evidence-store`
    already puts on screenshot evidence). Prompt-only — **no fine-tuning, no in-house training**.
- **Video as opt-in debugging evidence only**: a UI step MAY emit a screen recording when
  explicitly enabled. It is marked not-redaction-guaranteed and explicitly **not proof** — never
  part of any evidence hash or flag-proof adjudication — and it is analyzed only on explicit
  on-demand request, never automatically.
- **Dashboard** surfaces the visual-analysis classification, confidence, prose, and analyzer
  version stamp on the run/evidence view, clearly marked advisory and visually distinct from the
  pass/fail verdict.

## Capabilities

### New Capabilities

- `visual-analysis`: the opt-in, advisory vision-model triage layer over failed visual
  assertions — its fixed result schema, its strict non-authority over pass/fail, per-tenant
  isolation, model+prompt version stamping, graceful degradation, and the location-neutral
  contract that lets the same schema come from a hosted or a customer-hosted model.

### Modified Capabilities

- `ui-adapter`: adds the `ui.assertScreenshot` baseline-comparison assertion (deterministic
  pixel-diff verdict) and the baseline/actual/diff triplet it emits on failure, plus optional
  screen-recording emission from a UI step.
- `evidence-capture`: the screenshot triplet is captured and runs through the existing
  best-effort screenshot redaction before upload; video recording is added as an opt-in,
  not-redaction-guaranteed, explicitly-not-proof evidence kind.
- `evidence-store`: stored video recordings follow the same organization-scope and retention/purge
  terms as other evidence but are marked non-authoritative (never referenced by a flag-proof
  result); on-demand video analysis is initiated from stored evidence, not at upload.
- `dashboard`: the run/evidence view surfaces the advisory visual-analysis result, marked
  distinct from the deterministic verdict.

## Impact

- **`ReleaseTwin.Adapters.Ui`**: new `ui.assertScreenshot` operation, baseline storage/resolution,
  diff-image generation, triplet evidence emission, optional recording capture.
- **`ReleaseTwin.Core` / `evidence-capture`**: triplet passes through existing screenshot
  redaction; new video evidence kind with the not-proof / not-guaranteed-redacted markers; ensure
  no evidence-hash or flag-proof code path ingests a recording.
- **CLI**: an opt-in flag/config to enable visual analysis and (separately) recording; surfaces
  the returned advisory result in local output without affecting the exit code.
- **`hosted/ReleaseTwin.Hosted.Api`**: a new analysis endpoint that accepts a redacted triplet
  and returns the fixed-schema result; per-tenant isolation in request batching; storage of the
  result scoped to its report; an on-demand video-analysis trigger. New GPU/inference dependency
  (self-hostable Qwen2.5-VL-class model behind an OpenAI-compatible server) — see design.md.
- **`web/`**: dashboard run/evidence view renders the advisory result and analyzer version stamp.
- **Plan gating**: visual analysis and evidence video both require Paid tier, reusing the
  `plan-tier-gating` / `evidence-store` precedent.
- **Cost**: one always-on (or serverless) GPU; screenshot analysis ≈ fractions of a cent each
  batched; video analysis bounded by capped duration and frame sampling, on-demand only.
- **No change** to any existing ingest contract shape, the deterministic verdict of any existing
  assertion, or `ReleaseTwin.Core`'s failure classification.
