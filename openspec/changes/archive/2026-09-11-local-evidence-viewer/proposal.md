## Why

The CLI already writes the product's artifact to disk: with `RELEASETWIN_EVIDENCE_DIR` set,
every case produces `<dir>/<case-id>/evidence.json` plus redacted screenshot PNGs, and
`RELEASETWIN_UI_VIDEO_DIR` adds session video. There is no way to look at any of it. A user
who runs the CLI with no account gets a terminal line and a folder of JSON.

Playwright's HTML report and `trace.playwright.dev` are the pattern the whole category
copied: the tool's own artifacts open in a browser with zero account, so the free tool
explains itself, and the hosted product's pitch becomes "this, kept, shared, and rolled up".
The competitive review found this is the single cheapest self-discovery mechanic a
CLI-plus-dashboard product can ship, and ReleaseTwin has the artifact but not the viewer.

The platform repo carries the other half of this work — the quickstart and hosted-platform
docs, and the twin of the shared evidence fixture — under its own `local-evidence-viewer`
change. This change ships the viewer itself.

## What Changes

- **`releasetwin view [dir]`** — serves an evidence directory over HTTP on localhost and
  prints the URL, opening the default browser when the environment has one. Renders the same
  per-case drill-down the hosted dashboard shows: a case list ordered failed-first, and per
  case a step table with operation, outcome, and duration; assertion expression / expected /
  observed; adapter-emitted evidence; screenshots inline; the redaction note. Session video
  is shown when a matching `RELEASETWIN_UI_VIDEO_DIR` recording is present — the one thing
  the local view has that the hosted one does not. Pure static assets embedded in the CLI
  assembly; no network calls, no account, no telemetry.
- **`releasetwin view --export <file.html>`** — writes a single self-contained HTML file with
  the evidence and images inlined as data URIs, so a run's evidence can be attached to a PR
  or emailed without the CLI. This is also the path that works unchanged inside the container
  image, where there is no browser to open.
- **Container-aware serving.** `view` binds a port that can be published out of the container
  and always prints the URL rather than assuming a browser exists; it attempts to open one
  only when it is not running containerized. The usage text names the `-p` mapping the
  container form needs.
- **The GitHub Action uploads the viewer export as a workflow artifact** when
  `RELEASETWIN_EVIDENCE_DIR` is set, so a PR's evidence is one click from the check.
- **A shared evidence fixture**, byte-identical to the platform repo's copy at
  `web/src/test/fixtures/evidence-document.json`, with a test rendering the viewer against
  it — so the local viewer and the hosted drill-down cannot drift apart silently.

## Deliberately not in this change

- **Reusing the hosted dashboard's React components.** The engine is AGPL, .NET, and must
  work offline with no toolchain beyond the CLI image; the dashboard's components are BSL and
  would drag a Node build step into this repo. See Decision below.
- **Uploading anything.** `view` is read-only against a local directory and makes no network
  call of any kind, including no version check.
- **A hosted "upload this folder to view it" path.** That is the paid product; the point of
  this change is value before an account exists.
- **Indexing or serving multiple runs.** `view` renders one evidence directory's cases.
  Roll-up across runs is the hosted product's job.

## Decision and the alternative rejected

**Embed a static vanilla-JS viewer as assembly resources; do not reuse the Next.js dashboard
code.** The engine must work offline with no toolchain beyond the CLI image, and a small
dependency-free viewer compiled in as embedded resources keeps `dotnet tool install` and the
container image exactly as they are today.

*Rejected: `--export` only, no server.* A served view is what makes `init` → `run` → `view`
the ten-minute quickstart; the export is for sharing, and both are cheap once the rendering
is one set of static assets.

*Rejected: opening the local folder in the hosted dashboard ("upload to view").* That is the
paid product, and it defeats the purpose.

*Rejected: a second npm-built frontend in this repo.* It would add a Node build to an AGPL
.NET repo whose packaging contract is "no toolchain beyond the CLI image".

## Capabilities

### New Capabilities

- `local-evidence-viewer`: the CLI SHALL render a local evidence directory in a browser with
  no account and no network call, showing the same evidence document the hosted dashboard
  renders, and SHALL export a single-file HTML equivalent.

### Modified Capabilities

- `cli-packaging`: the published container image and .NET global tool SHALL both expose the
  `view` verb, and the image's documented usage SHALL cover publishing the viewer's port.
- `ci-pr-integration`: the GitHub Action SHALL upload the viewer export as a workflow
  artifact when the run wrote local evidence.

## Impact

- **`src/ReleaseTwin.Cli`:** a `view` branch in `CliEntrypoint`'s dispatch (ahead of the
  run fallthrough, which currently treats any unrecognized head as a directory to run),
  an evidence-directory reader, embedded static assets, a local HTTP server, and an export
  writer.
- **`integrations/github-action`:** an artifact-upload step.
- **`tests/ReleaseTwin.Cli.Tests`:** viewer rendering against the shared fixture, export
  self-containedness, and the directory-reader's tolerance of absent optional fields.
- **No** change to the run path, the redactor, `LocalEvidenceWriter`, or any upload code.
- **Platform repo:** docs and the twin fixture, in its own change and PR.

## Manual steps

None.
