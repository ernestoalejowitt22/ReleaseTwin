## Why

`local-evidence-artifacts` closed the gap that made this impossible: evidence
(screenshots) can now be written to a local directory with no hosted account. But every
failure demo in this repo — `docs/ci.md`'s Azure/Bitbucket screenshots, the example CI
apps — still only shows a *passing* run. Nothing in the repo shows what a **failure**
looks like with real, attached evidence next to it; today a reader only sees plain text
like `FAIL X (assertion): expected "a", observed "b"`. Since evidence capture only
produces screenshots via the UI adapter (HTTP-only cases have no visual surface), this
needs a UI-driven example case, not just a config change.

## What Changes

- Add a new example case that intentionally fails a UI assertion against a real, public
  page (reusing `examples/cases-ui-journey`'s existing target,
  `the-internet.herokuapp.com/login`, rather than introducing a new external dependency)
  — the failure is a deliberately wrong expected value in an `ui.assertText` step (e.g.
  asserting the login flash message equals text that isn't what the real page shows),
  not a rigged pipeline or a faked capture step. The case runs the real browser, the real
  assertion genuinely fails, and the CLI's real redaction/evidence pipeline produces a
  real screenshot of the real page at the moment of failure.
- Run that case with `RELEASETWIN_EVIDENCE=on` and `RELEASETWIN_EVIDENCE_DIR=<path>` (no
  `RELEASETWIN_API_TOKEN`) to produce `<path>/<case-id>/evidence.json` +
  `<path>/<case-id>/<screenshot-id>.png` locally, exercising `local-evidence-artifacts`
  end to end.
- Capture that resulting screenshot and wire it into `docs/ci.md`'s CI-portability
  narrative alongside a real `FAIL` line from the same run — replacing today's abstract
  "here's what a failure looks like" prose (if any) with an actual attached image next to
  actual `FAIL` text, following the same "the artifact is the deliverable" bar as
  `feature-proof-showcase` and `ci-docs-portability-screenshots`.
- Document the case's purpose clearly (a README note or inline case comment) as an
  intentional, illustrative failure — not a flaky or broken example — so it isn't
  mistaken for an actual bug in the demo suite.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — this exercises `ui-adapter` and `cli-runner`'s existing local-evidence behavior
as already specified; no requirement changes. `local-evidence-artifacts` already shipped
the write path this relies on.)

This change sets `skip_specs: true`: it adds an example case and documentation, not new
CLI/adapter behavior. If implementation reveals a genuine gap in the already-shipped
local-evidence capability, this proposal is paused and a separate change is opened for
the CLI fix — not folded in here.

## Impact

- **New example case** under `examples/cases-ui-journey/cases/` (or a new
  `examples/cases-demo-failure/` directory — confirmed in design.md), plus its fixture.
- **`docs/ci.md`**: new screenshot + accompanying real `FAIL` text wired into the CI
  narrative.
- **New file under `docs/assets/ci/`**: the captured failure screenshot.
- **No code changes** — this only runs the CLI against a new case file; no `src/` edits
  are expected. If one turns out to be needed, that's a signal to stop and open a
  separate change instead of scope-creeping this one.
