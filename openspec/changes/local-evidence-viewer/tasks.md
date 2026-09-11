## 1. Read the evidence directory

- [x] 1.1 Add an evidence-directory reader under `src/ReleaseTwin.Cli/Evidence/Viewer/` that
  enumerates `<dir>/<case-id>/` subdirectories, deserializes each `evidence.json` into the
  existing `EvidenceDocument` records, and lists that case's `<screenshot-id>.png` files.
- [x] 1.2 Make the reader tolerate the on-disk form: optional keys absent rather than null
  (`NullValueHandling.Ignore`), and a leg with no `leg` name. A case directory with an
  unreadable or malformed `evidence.json` is reported as that case failing to load, not as a
  crash and not as a silently missing case.
- [x] 1.3 Order cases failed-first: any case with a step whose outcome is a failure sorts ahead
  of the rest, stable within each group.
- [x] 1.4 Return a clear "path does not exist" / "no case directories found" result that the
  caller turns into a non-zero exit, rather than an empty report.

## 2. Render the report

- [x] 2.1 Add `EvidenceReportRenderer` producing the full HTML report from the reader's model
  (design D1) — no server, no file writes, so every rendering test is a string assertion.
- [x] 2.2 Render per case: case id, oracle locator, redaction note, and per leg the ordered
  steps with index, operation name, outcome, duration, assertion expression / expected /
  observed, and adapter evidence where present. Named legs become distinct labelled sections;
  a single unnamed leg renders as one unlabelled step sequence.
- [x] 2.3 Render screenshots inline, labelled best-effort-redacted, and carry the document's
  redaction note into the report.
- [x] 2.4 Add the viewer's CSS/JS under `Evidence/Viewer/Assets/` and ship them as
  `<EmbeddedResource>` in `ReleaseTwin.Cli.csproj`, read via `GetManifestResourceStream` the
  way `ScaffoldWriter` reads its templates (design D3).
- [x] 2.5 Render the case list as the left-hand navigation, failed cases first, matching the
  order task 1.3 establishes.

## 3. Session video

- [x] 3.1 Extract the video-filename sanitizer out of `ClosePageCleanup`'s private method in
  `ReleaseTwin.Adapters.Ui` into a shared helper both the recorder and the viewer call, with no
  behavior change (design D4).
- [x] 3.2 Add `--video-dir <dir>` to `view`, defaulting to `RELEASETWIN_UI_VIDEO_DIR` when set
  and to no video otherwise; match each case to `<video-dir>/<sanitized-case-id>.webm`.
- [x] 3.3 Render a matched recording inline with that case. A case with no recording renders
  exactly as if video was never enabled; an unreadable recording does not prevent the rest of
  that case's evidence from rendering.

## 4. Serve and export

- [x] 4.1 Add the `view` branch to `CliEntrypoint`'s dispatch **before** the run fallthrough —
  an unmatched head is currently treated as a directory of cases to run, so a missing branch
  would make `view` silently attempt a run.
- [x] 4.2 Serve the rendered report over `System.Net.HttpListener` (design D2) on port 8080,
  taking the next free port when it is in use and printing whichever it bound.
- [x] 4.3 Bind loopback normally and all interfaces when containerized
  (`DOTNET_RUNNING_IN_CONTAINER` or `/.dockerenv`), per design D6. Always print the URL; open a
  browser only when not containerized, and never fail the command because opening one failed.
- [x] 4.4 Add `--export <file.html>`: same renderer, written to a file instead of served, with
  screenshots inlined as `data:` URIs. Print the path written.
- [x] 4.5 Embed a matched recording in the export only when it is under the size cap; otherwise
  omit it and say so in the report (design D5, and the cap value is that doc's open question).
- [x] 4.6 Extend `PrintUsage` with `view`, including the `--export` and `--video-dir` flags and
  the container port-publishing note.

## 5. Pin the shared document shape

- [x] 5.1 Copy `evidence-document.json` and `evidence-document-flag-proof.json` verbatim from
  the platform repo's `web/src/test/fixtures/` into `tests/ReleaseTwin.Cli.Tests/Fixtures/`,
  and add a README naming the twin path and the byte-identical requirement (design D7).
- [x] 5.2 Add renderer tests asserting both fixtures render: identity fields, every step's
  values, named legs as distinct sections, the unnamed leg as one sequence, and a step whose
  optional keys are absent rendering without error.
- [x] 5.3 Assert the fixtures' on-disk properties directly — no `null` anywhere, each optional
  key absent from at least one step, the ordinary document's single leg unnamed, the flag-proof
  legs named `known-bad` then `known-good` — so a fixture that drifts to explicit nulls fails
  here rather than passing every other test.
- [x] 5.4 Verify the pin actually bites: mutate a field name in a fixture, confirm the tests
  fail, and restore it byte-identical.

## 6. GitHub Action artifact

- [x] 6.1 Add an `evidence` input to `integrations/github-action/action.yml`, defaulting to
  `"false"` so existing workflows are unchanged.
- [x] 6.2 When enabled, run the CLI with `RELEASETWIN_EVIDENCE=on` and
  `RELEASETWIN_EVIDENCE_DIR=/out/releasetwin-evidence` — `RUNNER_TEMP` is already mounted as
  `/out` and the workspace is mounted read-only, so no new mount is needed (design D8).
- [x] 6.3 Add the export step and an `actions/upload-artifact` step for the produced HTML file.
- [x] 6.4 Make both steps best-effort: a capture, export, or upload failure is a warning and
  never changes the run's verdict, the PR comment, the check run, or the job conclusion.
- [x] 6.5 Document the input in `integrations/github-action/README.md`, stating that evidence
  stays on the runner and leaves only as a workflow artifact — nothing is sent to a hosted
  service.

## 7. Docs and close out

- [x] 7.1 Document the `view` verb in the repo's CLI documentation, including the container
  form with a published port and the export as the no-port alternative (`cli-packaging`'s
  added requirement).
- [x] 7.2 Run `dotnet build ReleaseTwin.sln` and `dotnet test ReleaseTwin.sln`; report actual
  test counts.
- [x] 7.3 Run `openspec validate local-evidence-viewer --strict` and fix anything reported.
- [x] 7.4 Confirm the fixtures here are byte-identical to the platform repo's copies
  (`diff` both pairs) and that the platform change's task 4.4 can be checked off.
- [x] 7.5 Done — the work went onto `local-evidence-viewer-impl`, not the unrelated
  `cursor/point-action-mirror-at-org` branch, and landed as PR #144 (merged 2026-09-11).
- [x] 7.6 Done — PR #144 merged to `main` on 2026-09-11, so the platform repo's quickstart
  copy is no longer describing an unreleased verb. Note `releasetwin view` is on `main` but
  not yet in a tagged release or a published container image; the next release tag ships it.
