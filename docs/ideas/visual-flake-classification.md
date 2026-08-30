# Idea — visual flake classification (advisory vision-model triage)

**Status:** parked. Explored 2026-08-29, captured as an OpenSpec proposal, not applied. Retired
from `openspec/changes/` on 2026-08-30 so it doesn't read as queued work. Revisit only after the
first design-partner validation calls confirm this is the wedge worth leading with.

## The problem

UI tests are the flakiest tests a team owns, and the dominant cause is visual assertions failing
on rendering noise — font hinting, anti-aliasing, sub-pixel layout, animation timing — not real
regressions. A deterministic pixel diff proves *that* pixels changed; it can't tell a human
whether "the submit button disappeared" or "Chrome changed its text rendering." That triage is
manual and it's the single biggest time sink in a UI suite.

## The shape of the idea

Two parts, both additive to today's adapter:

1. **`ui.assertScreenshot`** — a first-class baseline assertion in the UI adapter. Normalized,
   anti-alias-tolerant pixel diff against a stored baseline with a declared per-step threshold.
   Within threshold → pass; beyond → fail; missing baseline → a distinct "baseline absent"
   failure (never a silent pass). On failure it emits the baseline / actual / diff **triplet** as
   step evidence, through the existing best-effort CLI-side screenshot redaction.

2. **`visual-analysis`** — an opt-in, Paid-tier, **advisory** layer that takes the redacted
   triplet from a failed visual assertion and returns a fixed schema: classification
   `{rendering-noise | real-regression | inconclusive}`, a confidence, a short prose explanation,
   and a `{modelId, promptVersion}` stamp. Plus an operational `analysis-unavailable` state.

## Non-negotiables (why it stayed "advisory")

- **The pixel diff is the only verdict.** Analysis never changes a run's pass/fail, its report
  outcome, its exit code, or a flag-proof adjudication. It's a sidecar record on the evidence
  entry, never read by core execution or classification.
- **Prompt-only, off-the-shelf.** An open-weights VLM (Qwen2.5-VL-7B class, 4-bit, vLLM with
  guided JSON decoding). No fine-tuning, no in-house training — the exploration concluded that
  isn't justified yet.
- **Location-neutral schema.** Identical result shape whether the model runs in the hosted
  service (first) or a customer-hosted model (later, the enterprise pitch). Downstream never
  branches on location.
- **Graceful degradation.** Analysis unavailable / errored / timed out leaves the run, report,
  and upload byte-for-byte unchanged; result recorded as `analysis-unavailable`.
- **Reuse existing machinery.** Same Paid-tier gate as `evidence-store`, same per-project
  retention/purge, same per-tenant scoping. One org per analyzer request — never batch across
  orgs.

## Video (deliberately second-class)

Opt-in screen recording as **debugging evidence only**: marked not-proof and
not-redaction-guaranteed at capture, excluded from every evidence hash and adjudication path,
stored (Paid tier) under the normal retention window, analyzed only via an authenticated
on-demand endpoint that frame-samples (~1 fps, downscaled) with a hard duration cap. Never
byte-deterministic across codecs, so it can't be hashed as proof.

## What it would touch

- `ReleaseTwin.Adapters.Ui` — new `ui.assertScreenshot` op, baseline storage/resolution, diff
  image generation, triplet emission, optional recording capture.
- `ReleaseTwin.Core` / `evidence-capture` — triplet through existing redaction; new video
  evidence kind with the not-proof markers; assert no hash/flag-proof path ingests a recording.
- `hosted/ReleaseTwin.Hosted.Api` — a new analysis endpoint (`{orgId, reportRef, assertionRef,
  baseline, actual, diff}` → fixed schema), per-tenant isolation, result storage scoped to the
  report, an on-demand video-analysis trigger. **New GPU/inference dependency** (serverless GPU
  acceptable given bursty load) — this is the real cost of the idea.
- `web/` — dashboard renders the advisory result + version stamp, visually distinct from and
  subordinate to the pass/fail verdict.
- CLI — opt-in flags for analysis and (separately) recording.

## Rollout gate

A held-out labelled set (100–300 real triplets from design-partner UI suites) scored for
classification accuracy / per-class precision, plus an LLM-judge rubric for prose quality, with a
specific eye on the **false-noise rate** (model says "noise" on a real regression). Re-run on any
prompt or model bump; the version stamp makes results attributable. This gates enabling the
feature for anyone.

## Why it's parked

- Multi-week build with a genuinely new hosted dependency (GPU inference), for a feature with
  zero external validation.
- `docs/self-serve-funnel-plan.md` explicitly defers the "is visual flake classification the
  wedge the landing page should lead with?" question until after the first validation calls — it
  has a crisper problem statement and an existing category, but that's an unproven hypothesis.
- The deterministic-verdict guarantees are the hard part of the design and they're captured here;
  picking this back up is a proposal-refresh, not a from-scratch exploration.

## Open questions if revisited

- Pixel-diff library + threshold defaults for `ui.assertScreenshot` (implementation detail).
- Whether the first release includes the bounded synchronous retry-hint wait (a `rendering-noise`
  verdict informing a retry the case policy *already* permits — never adding retries) or ships
  async-only with no retry influence.
- Frame-sampling rate and duration cap constants for on-demand video analysis.
- Baseline management (stale baselines, per-environment rendering) — out of scope beyond "missing
  baseline is a clear failure"; a baseline-update workflow would be its own change.
