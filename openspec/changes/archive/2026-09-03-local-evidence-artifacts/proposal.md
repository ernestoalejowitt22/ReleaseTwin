## Why

Evidence capture (screenshots, per-step action logs) only runs today when a hosted
ingest token is configured (`CliRunner.cs:415`: `CaptureEvidence = captureEvidence &&
ingestClient is not null`) — there is no way to get a redacted evidence artifact onto
disk without a paid hosted project. This makes every failure in a public, unauthenticated
context (this repo's own CI docs, the `releasetwin-ci-examples` demo apps, anyone
evaluating the CLI without an account) show up as plain pass/fail text with nothing to
look at. Video already has a local-only path (`RELEASETWIN_UI_VIDEO_DIR`,
`CliRunner.cs:339`) with no upload requirement; screenshots and action-log evidence have
no equivalent. This change closes that gap so a case's redacted evidence can be written
to a local directory regardless of hosted configuration, reusing the existing
`EvidenceRedactor` / `EvidenceDocument` / `RedactedScreenshot` pipeline rather than
building a second one.

This is a prerequisite for a follow-up change (not part of this one) that adds a richer,
intentionally-failing demo case to `docs/ci.md` and the CI example apps with a real
attached screenshot — that work needs this capability to exist first.

## What Changes

- Add a local evidence output path: when a `RELEASETWIN_EVIDENCE_DIR` env var (mirroring
  the existing `RELEASETWIN_UI_VIDEO_DIR` naming convention) is set, the CLI writes each
  captured case's redacted evidence there — the `EvidenceDocument` as JSON and each
  `RedactedScreenshot` as a PNG — after passing through the same redaction pipeline used
  before hosted upload.
- Decouple evidence **capture** from evidence **upload**: `RELEASETWIN_EVIDENCE=on`
  becomes actionable with a local evidence dir alone, with no `RELEASETWIN_API_TOKEN`
  required. Today's behavior (`captureEvidence && ingestClient is not null`) becomes
  `captureEvidence && (ingestClient is not null || localEvidenceDir is not null)`.
  Hosted upload behavior for users who do have a token is unchanged — this only adds a
  path for users who don't. Local file writing happens once per case, alongside (not
  instead of) any configured upload — if both a local dir and a hosted token are
  configured, both happen.
- File naming/layout: one subdirectory per case id under `RELEASETWIN_EVIDENCE_DIR`,
  containing `evidence.json` and any screenshot PNGs referenced by id. Exact layout is a
  design.md decision, not decided here.
- No new adapter surface — this only changes when/where the CLI writes the
  `RedactionResult` that `EvidenceRedactor.Redact(...)` already produces; the UI adapter,
  redaction rules, and the hosted upload path are unchanged.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `cli-runner`: the existing requirement "Captured evidence is redacted then uploaded"
  already anticipated this — it says the CLI *MAY* still surface evidence locally without
  a token, but that MAY was never implemented, and the actual code gates capture itself
  on `ingestClient is not null` (`CliRunner.cs:415`), which also contradicts the separate
  "Evidence capture is a resolved opt-in, off by default" requirement's own scenarios
  (opt-in is described as independent of whether a token is configured). This change
  turns that MAY into a concrete SHALL for the `RELEASETWIN_EVIDENCE_DIR` case and fixes
  the capture gate to match the opt-in requirement's existing text.
  `evidence-capture`'s own requirements (redaction, redaction-before-transmit) are
  unaffected — local output goes through the identical redaction step as upload does, so
  no change needed there.

## Impact

- **`src/ReleaseTwin.Cli/CliRunner.cs`**: read a new env var, decouple the
  `CaptureEvidence` gate from `ingestClient`, add a local-write call alongside the
  existing per-case upload call sites (~line 415, ~492-516, ~536-554).
- **New code**: a small local evidence writer (file layout TBD in design.md) — likely a
  new file under `src/ReleaseTwin.Cli/Evidence/`.
- **`openspec/specs/cli-runner/spec.md`**: requirement text updates described above.
- **No adapter changes.** No hosted/`releasetwin-platform` changes — this is entirely
  within the public CLI.
- **No breaking changes** — existing hosted-upload behavior with a token configured is
  unchanged; this only adds behavior when a new env var is set.
