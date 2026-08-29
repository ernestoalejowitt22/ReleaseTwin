## 1. `ui.assertScreenshot` deterministic assertion (UI adapter)

- [ ] 1.1 Add a `ui.assertScreenshot` operation to `ReleaseTwin.Adapters.Ui`: capture full-page or a declared element/region render as PNG
- [ ] 1.2 Implement baseline resolution per step (stored baseline lookup) and a normalized, anti-alias-tolerant pixel diff with a declared per-step threshold
- [ ] 1.3 Verdict wiring: within-threshold → pass, beyond-threshold → fail, missing baseline → fail with a distinct "baseline absent" reason (never silent pass / crash)
- [ ] 1.4 Ensure the failure is classified and cleanup runs exactly like any other UI step failure (reuse existing pipeline integration)
- [ ] 1.5 On failure, produce a diff image highlighting changed regions and expose baseline + actual + diff as the step's evidence payload
- [ ] 1.6 Unit tests: pass, fail, missing-baseline, threshold boundary, cleanup-after-fail

## 2. Triplet capture and redaction (evidence-capture)

- [ ] 2.1 Route the baseline/actual/diff triplet through the existing best-effort screenshot redaction (declared region/selector masks) before any persistence or upload
- [ ] 2.2 Mark each of the three images `best-effort-redacted` in the evidence document
- [ ] 2.3 Guarantee no code path exposes a pre-redaction image to upload or to analysis (assert in code + test)
- [ ] 2.4 When evidence capture is disabled, persist/upload no triplet; when enabled but analysis disabled, still no analysis call
- [ ] 2.5 Tests: masked triplet upload, capture-disabled produces nothing, pre-redaction image never leaves

## 3. `visual-analysis` result model and schema

- [ ] 3.1 Define the fixed result schema: classification `{rendering-noise|real-regression|inconclusive}`, confidence, prose explanation, `{modelId, promptVersion}` stamp; plus the operational `analysis-unavailable` state
- [ ] 3.2 Attach the result to the failed assertion's evidence entry as a separate advisory field, never read by core execution / classification / flag-proof
- [ ] 3.3 Make the schema identical for hosted and (future) customer-hosted analyzers — single shared type, no location branching
- [ ] 3.4 Serialize the result into the evidence document and the ingest payload
- [ ] 3.5 Tests: schema round-trip, result absent by default, flag-proof adjudication ignores the result

## 4. CLI opt-in and orchestration

- [ ] 4.1 Add opt-in config/flags: enable visual analysis; enable screen recording (independent of screenshot evidence)
- [ ] 4.2 After a failed visual assertion (analysis enabled), submit the redacted triplet to the configured analyzer endpoint; enforce a short time budget
- [ ] 4.3 On timeout/error/unavailable, record `analysis-unavailable` and leave run, report, exit code, upload byte-for-byte unchanged
- [ ] 4.4 Retry-hint path: allow a bounded synchronous wait within the step's existing timeout so a `rendering-noise` verdict can inform a retry the case policy already permits; never add retries or extend timeouts
- [ ] 4.5 Surface the advisory result in local CLI output without affecting the exit code
- [ ] 4.6 Tests: analysis-disabled unchanged-run, analyzer-down graceful degradation, noise-hint respects declared retry count, no-declared-retries → no retry

## 5. Hosted analyzer service

- [ ] 5.1 Stand up a vLLM server with an open-weights VLM (Qwen2.5-VL-7B class, 4-bit) and JSON/guided decoding; single GPU or serverless GPU
- [ ] 5.2 Version the prompt + few-shot examples as an asset; expose `{modelId, promptVersion}` for stamping
- [ ] 5.3 New hosted endpoint: accept `{orgId, reportRef, assertionRef, baseline, actual, diff}` (already redacted), return the fixed schema
- [ ] 5.4 Per-tenant isolation: exactly one organization per analyzer request; never batch across orgs
- [ ] 5.5 Paid-tier gate on acceptance (reuse the `evidence-store` entitlement check); Free-tier → distinct non-fatal rejection, run otherwise unaffected
- [ ] 5.6 Store the result scoped to its report/organization; readable only within that organization
- [ ] 5.7 Behind a feature flag, default off
- [ ] 5.8 Tests: isolation (no cross-org batch), Paid gate, Free-tier rejection non-fatal, result org-scoping

## 6. Evaluation harness

- [ ] 6.1 Assemble a held-out labelled set of 100–300 real triplets from design-partner UI suites
- [ ] 6.2 Score classification accuracy and per-class precision/recall; LLM-judge rubric for prose quality; report false-noise rate specifically
- [ ] 6.3 Wire the eval to run on any prompt/model version bump and record results against the version stamp
- [ ] 6.4 Set a go/no-go threshold for enabling the feature

## 7. Video evidence (opt-in, non-authoritative)

- [ ] 7.1 UI adapter: emit a screen recording when recording is explicitly enabled, independent of screenshot evidence; no influence on any outcome
- [ ] 7.2 evidence-capture: mark recordings not-redaction-guaranteed and not-proof; exclude from every evidence hash; never auto-submit to analysis during a run
- [ ] 7.3 evidence-store: store recordings scoped to project/org, subject to the project retention window and purge, marked non-authoritative
- [ ] 7.4 Assert no flag-proof result or report outcome references a recording, and recording expiry/absence never changes an adjudication
- [ ] 7.5 On-demand video-analysis endpoint: authenticated, org-scoped, frame-sampled (~1 fps, downscaled) with a hard duration cap; result stored advisory against the report; never runs at upload/purge
- [ ] 7.6 Tests: recording opt-in gating, excluded-from-hash, purge on window, on-demand-only, adjudication unaffected by expiry

## 8. Dashboard

- [ ] 8.1 Render the advisory visual-analysis result on the evidence view: classification, confidence, prose, `{modelId, promptVersion}` stamp
- [ ] 8.2 Make it visually distinct from and subordinate to the pass/fail verdict; label it advisory
- [ ] 8.3 Handle `analysis-unavailable` (show as unavailable, assertion still failed) and no-result (verdict only)
- [ ] 8.4 Surface stored recordings with a "trigger analysis" action for on-demand video analysis
- [ ] 8.5 Tests / component checks for each display state

## 9. Docs and rollout

- [ ] 9.1 Update `docs/installation-model.md` (and README evidence section) to describe opt-in visual analysis, the Paid-tier gate, redaction posture, and that video is not proof
- [ ] 9.2 Document the location-neutral result schema for a future customer-hosted analyzer
- [ ] 9.3 Staged rollout per design.md Migration Plan; verify rollback (feature flag off → pure pixel-diff assertion, results revert to `analysis-unavailable`)
