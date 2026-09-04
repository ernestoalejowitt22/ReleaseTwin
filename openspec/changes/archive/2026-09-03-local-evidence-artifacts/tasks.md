## 1. Local evidence writer

- [x] 1.1 Add `LocalEvidenceWriter` under `src/ReleaseTwin.Cli/Evidence/`: takes a case
      id and a `RedactionResult`, writes `<dir>/<case-id>/evidence.json` (same JSON
      serialization the upload path already uses) and
      `<dir>/<case-id>/<screenshot-id>.png` for each `RedactedScreenshot`.
- [x] 1.2 Creates the case subdirectory if missing; overwrites existing files for a
      re-run of the same case id (no append/versioning).

## 2. CliRunner wiring

- [x] 2.1 Read `RELEASETWIN_EVIDENCE_DIR` alongside the existing `RELEASETWIN_UI_VIDEO_DIR`
      read (`CliRunner.cs:339`).
- [x] 2.2 Fix the capture gate: `CaptureEvidence = captureEvidence` (drop
      `&& ingestClient is not null`, `CliRunner.cs:415`).
- [x] 2.3 At the flag-proof call site (`CliRunner.cs:492-516`): after
      `redactor.Redact(...)` produces `evidence`, when a local evidence directory is
      configured and `evidence` is non-null, call the writer with the case id; wrap in
      try/catch mirroring the existing upload failure handling, emit a `WARN:` line on
      failure, do not affect the case outcome or exit code.
- [x] 2.4 At the normal-case call site (`CliRunner.cs:536-554`): same as 2.3.
- [x] 2.5 Confirm both the local write and the upload call are independently gated
      (`if (ingestClient is not null)` for upload, a separate `if` for the local
      directory) so either, both, or neither can run per the modified spec's scenarios.

## 3. Tests

- [x] 3.1 Unit test: `RELEASETWIN_EVIDENCE_DIR` set, no API token — evidence is written
      locally, no upload attempted (network handler receives zero calls).
- [x] 3.2 Unit test: both `RELEASETWIN_EVIDENCE_DIR` and a token configured — both the
      local write and the upload happen for the same run.
- [x] 3.3 Unit test: evidence capture enabled, neither destination configured — run
      completes normally, no local files written, no upload attempted, no warning
      (this isn't a failure, just a no-op destination set).
- [x] 3.4 Unit test: local directory not writable (e.g. a file exists where the case
      subdirectory would go) — case still reported with its correct pass/fail outcome,
      exit code unaffected, a `WARN:` line is emitted, and — when a token is also
      configured — the upload still proceeds.
- [x] 3.5 Unit test: flag-proof case with local evidence directory configured — one
      `evidence.json` is written under the case's id containing both legs (known-bad and
      known-good), matching the existing `RedactionResult` shape.
- [x] 3.6 Unit test: two cases in one run, both producing evidence, local directory
      configured — each case's files land under its own case-id subdirectory, neither
      overwrites the other.
- [x] 3.7 Regenerate/confirm existing evidence-capture and cli-runner test suites still
      pass with the gate fix (2.2) — in particular, confirm no existing test relied on
      the old (buggy) `captureEvidence && ingestClient is not null` gate. Full solution:
      `dotnet test ReleaseTwin.sln` — 300/300 passing across all projects.

## 4. Docs & spec sync

- [x] 4.1 Document `RELEASETWIN_EVIDENCE_DIR` (`RELEASETWIN_UI_VIDEO_DIR` itself turned out
      to be undocumented anywhere — no doc to sit "alongside"). Updated
      `examples/cases-ui-journey/README.md`, which had the actual stale claim ("with an
      API token, on a Paid-tier project" to capture a screenshot), and added a bullet to
      `docs/ci.md`'s Credentials section.
- [x] 4.2 `openspec validate local-evidence-artifacts --strict` passes.
