## Context

See proposal.md — Why. The constraints that actually shape the approach, all verified in the
code:

- **`ReleaseTwin.Cli` is a plain `net8.0` console `Exe`** with no ASP.NET Core framework
  reference, published two ways: the container image and a `PackAsTool` global tool
  (`src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj`). The global tool is framework-dependent by
  design — its users have the .NET runtime, not necessarily the ASP.NET Core shared runtime.
- **Embedded resources are already the convention** — `<EmbeddedResource
  Include="Scaffolding\Templates\*" />`, read through
  `Assembly.GetExecutingAssembly().GetManifestResourceStream` in `ScaffoldWriter`.
- **`CliEntrypoint.RunAsync` dispatches on `args[0]`**, and anything it does not recognize
  falls through to the run path, where it is treated as *a directory of cases to run*. A new
  verb must be matched before that fallthrough, and an unmatched verb is silently a run
  target — which is why `view` has to be an explicit branch, not a default.
- **On-disk evidence layout:** `LocalEvidenceWriter` writes
  `<dir>/<case-id>/evidence.json` plus `<dir>/<case-id>/<screenshot-id>.png`, serializing with
  `NullValueHandling.Ignore` — so optional fields are *absent keys*, not nulls.
- **Video lives somewhere else entirely.** `ClosePageCleanup.FinalizeVideosAsync` saves each
  recording as `<RELEASETWIN_UI_VIDEO_DIR>/<sanitized-case-id>.webm` — a flat directory, no
  per-case subdirectory, and *nothing in `evidence.json` references it*. The sanitizer maps
  any character outside `[A-Za-z0-9-_.]` to `-`, and is currently a `private static` method on
  `ClosePageCleanup`.
- **The hosted twin** is `web/src/test/fixtures/evidence-document.json` (and its flag-proof
  sibling) in the platform repo, already rendered under test there.

## Goals / Non-Goals

**Goals:**

- One renderer used by both the served view and the export, so the two cannot disagree.
- Rendering testable without opening a socket or launching a browser.
- No new runtime prerequisite for either published form.
- The shared document shape pinned against the platform repo's fixture.

**Non-Goals:**

- A general-purpose static file server. `view` serves one rendered report and its assets.
- Watching the directory for changes, or live-reloading while a run is in progress.
- Authentication on the served report. It is a local process over loopback; see Risks for the
  containerized case.
- Rendering anything the evidence document does not contain. If a field is not in the
  document, the viewer does not go looking for it — video is the single, deliberate exception.

## Decisions

**D1 — Split rendering from serving.** An `EvidenceReportRenderer` takes a read model of the
directory and returns HTML; `view` serves that string, `--export` writes it to a file. Every
rendering requirement is then testable as a string assertion with no socket, no port, and no
browser — and the served and exported reports are identical by construction rather than by
two code paths kept in step.

*Alternative rejected: render inside the request handler.* It makes every rendering test an
integration test against a live listener, and lets the export drift from the served view.

**D2 — Serve with `System.Net.HttpListener`, not Kestrel.** Kestrel needs
`<FrameworkReference Include="Microsoft.AspNetCore.App" />`, which requires the ASP.NET Core
shared runtime on the machine. That would break `dotnet tool install -g releasetwin` for users
who have only the .NET runtime, and it contradicts the packaging spec's promise that this
subcommand adds no runtime prerequisite. `HttpListener` is in the base framework, needs no
package, and serving one rendered document plus a few assets is exactly the scale it suits.

*Alternative rejected: Kestrel.* Better server, wrong dependency for a CLI whose distribution
contract is "no toolchain beyond the published artifact".

*Alternative rejected: a raw `TcpListener` and hand-rolled HTTP.* Avoids the dependency
question entirely and introduces a hand-written HTTP parser, which is strictly worse.

**D3 — Static assets as embedded resources, following the scaffolding precedent.** The
viewer's CSS and JS live under `Evidence/Viewer/Assets/` and ship via `<EmbeddedResource>`,
read the same way `ScaffoldWriter` reads its templates. No Node build, no separate download,
nothing to keep in sync at release time.

**D4 — The video directory is an explicit input, defaulting to the environment variable.**
Because `evidence.json` carries no reference to a recording and the recordings live in a
different directory, the viewer cannot discover video from the evidence directory alone. So
`view` takes `--video-dir <dir>`, defaulting to `RELEASETWIN_UI_VIDEO_DIR` when that is set
and to no video otherwise. A case is matched to `<video-dir>/<sanitized-case-id>.webm` using
the *same* sanitizer the recorder used.

To guarantee "the same sanitizer" rather than "a sanitizer that looks the same", the rule
moves out of `ClosePageCleanup`'s private method into a single shared helper that both the
recorder and the viewer call. Duplicating the character rule in the CLI would be a silent
mismatch the first time either side changed.

*Alternative rejected: guess a sibling directory by convention* (e.g. `../videos`). It would
work for the layout we document and fail silently for every other one.

*Alternative rejected: write the video path into `evidence.json`.* That is the clean fix, and
it changes the shared document shape — the one thing this change exists to hold still. It
also puts a local filesystem path into a document that gets uploaded. Deliberately deferred.

**D5 — The export embeds screenshots always and video only under a size cap.** Screenshots
are small and central, so they become `data:` URIs unconditionally. A session recording is
routinely tens of megabytes, and base64 inflates it by a third; embedding one unconditionally
produces a file too large to attach to the pull request the export exists to serve. So the
export embeds a recording when it is under a documented cap and otherwise omits it, saying in
the report that video was omitted and where to find it. The served view has no such limit — it
streams the file from disk.

*Alternative rejected: always embed.* Produces multi-hundred-megabyte "single files".

*Alternative rejected: never embed video in the export.* Simpler, and throws away the case
where the recording is small enough to be the most useful thing in the attachment.

**D6 — Bind loopback normally, all interfaces only when containerized.** A report served on
`0.0.0.0` is readable by anything on the network, and this report is deliberately unauthenticated,
so the default binding is loopback. Inside a container loopback is unreachable from the host
even with `-p`, so when the CLI detects it is containerized (`DOTNET_RUNNING_IN_CONTAINER`,
which the Microsoft base images set, or `/.dockerenv`) it binds all interfaces instead — the
network exposure there is already governed by whether the operator published the port.

The URL is always printed. A browser is opened only when not containerized, and a failure to
open one is never an error — the printed URL is the contract, per the spec.

Default port 8080, matching the platform repo's documented `-p 8080:8080`. When it is taken,
the CLI takes the next free port and prints that — failing because something unrelated holds a
port would be a poor first experience for a command whose whole job is "show me the thing".

**D7 — Fixtures live at `tests/ReleaseTwin.Cli.Tests/Fixtures/` and are byte-identical twins.**
`evidence-document.json` and `evidence-document-flag-proof.json`, copied verbatim from the
platform repo's `web/src/test/fixtures/`. A test asserts the on-disk properties the platform
repo's test also asserts — optional keys absent rather than null, the ordinary document's
single leg unnamed, the flag-proof document's legs named `known-bad` then `known-good` — so
each repo independently fails if its own copy drifts, and a README in each names the other.

*Alternative rejected: fetch the fixture from the other repo at test time.* Couples two repos
that deliberately do not depend on each other and turns a network hiccup into a red build.

**D8 — The Action captures into `RUNNER_TEMP` and uploads from there.** The Action already
mounts the workspace read-only and `RUNNER_TEMP` as `/out`, so evidence capture needs no new
mount: run with `RELEASETWIN_EVIDENCE=on` and `RELEASETWIN_EVIDENCE_DIR=/out/releasetwin-evidence`,
then a second container invocation runs `view --export /out/releasetwin-evidence.html`, then
`actions/upload-artifact` uploads that file. A new `evidence` input defaults to `"false"`, so
every existing workflow is untouched.

Both new steps are best-effort: the run step already refuses to fail the job on a case
failure, and the export and upload must not change the verdict either.

*Alternative rejected: keying off `RELEASETWIN_EVIDENCE_DIR` being set,* as the proposal
originally put it. Action users do not control the container invocation — the Action builds it
— so there is no such variable for them to set. It has to be an input.

## Risks / Trade-offs

**An unauthenticated report on all interfaces inside a container** → Mitigated by D6's
loopback default outside containers, and bounded inside one by the operator's own decision to
publish the port. Documented rather than solved; adding auth to a local viewer would defeat
the zero-friction point of it.

**`HttpListener` is a thin, lightly-maintained API on Linux** → It is a managed implementation
on non-Windows and adequate for localhost single-client serving. D1 keeps the blast radius
small: if it ever proves inadequate, the renderer is untouched and only the serving shell
changes.

**The video sanitizer becomes shared API** → A small public surface in the Ui adapter that did
not exist before, and the alternative is a duplicated rule that silently desynchronizes. The
shared helper is the lesser cost.

**Two case ids can sanitize to the same video filename** (e.g. `A/B` and `A-B`) → Pre-existing
in the recorder; the viewer inherits it. The viewer will show the same recording for both
rather than pretending to resolve it. Not introduced here, not fixed here.

**The fixture twins can drift between the two PRs** → Each repo's test pins its own copy, so
drift is caught the next time either shape changes. Accepted, as in the platform repo's D5.

**Documented before released** → The platform repo's quickstart documents `view` ahead of this
change shipping, by explicit decision. The sequencing gate lives on that change's task list;
this change's release is what unblocks it.

## Migration Plan

Additive throughout — a new verb, new embedded assets, a new opt-in Action input. No existing
behavior, output, exit code, or file layout changes, so there is nothing to migrate and
rollback is reverting the commit. The one non-additive edit is extracting the video-filename
sanitizer (D4), which preserves behavior exactly.

## Open Questions

- **The exact export video size cap (D5).** A number has to be picked; any value in the tens
  of megabytes satisfies every requirement here, and changing it later changes no contract.
