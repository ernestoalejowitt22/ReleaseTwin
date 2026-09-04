## Context

See proposal.md for motivation. Relevant existing code:

- `CliRunner.cs:415`: `CaptureEvidence = captureEvidence && ingestClient is not null` —
  the bug this change fixes; capture itself must not depend on `ingestClient`.
- **Found during implementation**: the same bug also lives one layer up — the block that
  *resolves* `captureEvidence` from `RELEASETWIN_EVIDENCE`/hosted default
  (`CliRunner.cs:218` originally) was itself wrapped in `if (ingestClient is not null)`,
  so `RELEASETWIN_EVIDENCE=on` was silently ignored with no token even before reaching
  line 415. Both gates needed fixing for the opt-in requirement's own scenarios (env
  opt-in works "regardless of the hosted per-project default") to actually hold; the
  hosted-default fallback fetch stays gated on `ingestClient is not null` since it
  requires the token to call the hosted API, but only as an `else if`, after the
  explicit env toggle is checked unconditionally.
- `CliRunner.cs:492-516` (flag-proof) and `:536-554` (normal case): both call
  `redactor.Redact(...)` to get a `RedactionResult`, then `ingestClient.UploadCaseReportAsync`
  / `UploadFlagProofReportAsync`, gated on `if (ingestClient is not null)`, inside the
  per-case loop.
- `Evidence/EvidenceDocument.cs`: `RedactionResult(EvidenceDocument Document,
  IReadOnlyList<RedactedScreenshot> Screenshots)` — `Document` is a plain
  JSON-serializable record; `Screenshots` carries `(string Id, byte[] PngBytes)` pairs,
  kept out-of-band from the document (only an id ref appears in the JSON).
- `RELEASETWIN_UI_VIDEO_DIR` (`CliRunner.cs:339`) is the existing precedent for a
  local-only, hosted-independent output path.

## Goals / Non-Goals

**Goals:**
- A `RedactionResult` produced for a case can be written to a local directory with no
  hosted config, at the same point in the per-case flow where it's currently uploaded.
- Fix the `CaptureEvidence` gate so capture happens whenever opted in, independent of
  which destination(s) are configured — matching the (already-written, never
  implemented) `cli-runner` spec text.

**Non-Goals:**
- No demo/docs wiring (follow-up change).
- No change to redaction rules, the UI adapter, or the hosted upload contract.
- No retention/cleanup policy for the local directory — the CLI writes into whatever
  directory is configured; managing/pruning it is the caller's responsibility (same as
  `RELEASETWIN_UI_VIDEO_DIR` today).

## Decisions

**Env var name: `RELEASETWIN_EVIDENCE_DIR`.** Matches the existing
`RELEASETWIN_UI_VIDEO_DIR` naming pattern exactly (`RELEASETWIN_<THING>_DIR`) rather than
inventing a `--evidence-dir` CLI flag — this repo's local-output precedent is
env-var-only, and case-loading env resolution (`ResolveEnvironmentVariable`, including
hosted-secret fallback) already flows through `Get(...)` the same way
`RELEASETWIN_UI_VIDEO_DIR` does. Alternative considered: a CLI flag — rejected for
consistency with the one existing local-output precedent and because CI usage (the
motivating case) sets env vars, not CLI flags, everywhere else in this repo's examples.

**Layout: `<dir>/<case-id>/evidence.json` + `<dir>/<case-id>/<screenshot-id>.png`.**
One subdirectory per case id, satisfying the new "organized per case" requirement
directly — no run-id or timestamp prefix, since a single CLI invocation runs each case
id at most once (flag-proof mode still produces one `RedactionResult` per case, covering
both legs inside the one document). Alternative considered: flat files prefixed with the
case id (`<dir>/<case-id>.json`) — rejected because screenshots would then need
separate id-collision-safe naming at the top level; a subdirectory keeps a case's
document and its screenshots visually grouped, which matters since this directory is
meant to be opened by a human (that's the whole point of this change).

**Write path: a new `LocalEvidenceWriter` in `src/ReleaseTwin.Cli/Evidence/`,** called
right after `redactor.Redact(...)` at both existing call sites, taking the already-built
`RedactionResult` and the case id — no new redaction logic, no new gating logic beyond
"is `RELEASETWIN_EVIDENCE_DIR` set". Serializes `Document` with the same JSON settings
the upload path already uses (reuse, don't reinvent, the existing serialization) and
writes each `RedactedScreenshot.PngBytes` verbatim.

**Gate fix: `CaptureEvidence = captureEvidence`** (drop `&& ingestClient is not null`).
The two per-case call sites already individually check `if (ingestClient is not null)`
before uploading — that check stays, unchanged, as the upload gate. A new, independent
check for the local directory sits next to it. This directly fixes the spec/code
mismatch described in proposal.md without touching how the opt-in itself
(`envEvidenceToggle` / hosted per-project default) is resolved.

**Failure isolation: local write failures caught and warned exactly like upload
failures today** (`catch` around the write, `output.WriteLine("WARN: ...")`, no effect
on exit code or case outcome) — mirrors the existing upload failure handling
(`CliRunner.cs:512-513` pattern) rather than introducing a different error-handling
shape for the second destination.

## Risks / Trade-offs

- **Disk usage**: a run with many UI-journey cases and evidence enabled could write a
  lot of screenshots locally with no automatic cleanup → acceptable, same trade-off as
  `RELEASETWIN_UI_VIDEO_DIR` already accepts; documented as the caller's responsibility.
- **Fixing the `CaptureEvidence` gate changes existing behavior for anyone currently
  relying on the (buggy) token-gated capture** — today, setting `RELEASETWIN_EVIDENCE=on`
  with no token silently captures nothing; after this fix, redaction runs internally but
  still produces no output with neither destination configured (matches the new
  "Capture is enabled with no token and no local evidence directory" scenario), so
  observable output-file behavior for existing users is unchanged — only wasted
  internal work (capture-then-discard) is added when evidence is opted into with no
  destination at all, which is a pre-existing possible misconfiguration, not a new one.
