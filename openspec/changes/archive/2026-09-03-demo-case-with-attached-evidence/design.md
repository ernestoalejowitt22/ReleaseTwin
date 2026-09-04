## Context

See proposal.md for motivation. Relevant existing state:

- `examples/cases-ui-journey/cases/example-ui-journey.yaml` already drives
  `the-internet.herokuapp.com/login` with `ui.navigate` → `ui.fill` (x2) → `ui.click` →
  `ui.waitFor` → `ui.assertVisible`, capturing the post-login `#flash` message text. It
  passes today.
- `ui.assertText` (`src/ReleaseTwin.Adapters.Ui/UiOperations.cs:187`) asserts an
  element's text `equals` or `contains` a given value — this is the operation that can
  produce a genuine, honest mismatch (real observed text vs. a wrong expected value)
  without touching any pipeline internals.
- `local-evidence-artifacts` (archived 2026-09-03) already ships
  `RELEASETWIN_EVIDENCE_DIR`: a case run with it set writes
  `<dir>/<case-id>/evidence.json` + `<dir>/<case-id>/<screenshot-id>.png`, independent of
  any hosted token. Nothing further is needed from the CLI for this change.
- `docs/assets/ci/` (from `ci-docs-portability-screenshots`) already holds 5 real
  screenshots referenced by `docs/ci.md`; this change adds a 6th, same convention.

## Goals / Non-Goals

**Goals:**
- One new case that fails for a real, honest, and permanent reason (not flaky, not
  timing-dependent) and produces a real local screenshot via the already-shipped
  capability.
- That screenshot, plus the real `FAIL` line from the same run, land in `docs/ci.md` next
  to each other.

**Non-Goals:**
- No CLI/adapter code changes. If the local-evidence write path turns out to have a gap
  when actually exercised against a real UI failure, stop and open a separate change —
  this proposal's scope is docs + one example case only.
- No new external target site — reuse `the-internet.herokuapp.com`, already the trusted
  target for `examples/cases-ui-journey`, rather than adding a second public dependency.
- No automation to re-capture this screenshot in CI — same one-time, manually-triggered
  convention as `ci-docs-portability-screenshots` (the target page's exact rendering
  could drift; the point is a real capture exists, not a live-updating one).

## Decisions

**The failure: a deliberately wrong expected value in `ui.assertText`, not a broken
selector or a rigged step.** The real page shows `You logged into a secure area!` after
a successful login (the same flow `examples/cases-ui-journey` already exercises
successfully). The new case adds one more step after the existing `ui.assertVisible`:
`ui.assertText` on `#flash` asserting `equals: "Welcome back, valid user!"` — text the
page never shows. This is honest in the sense the proposal requires: the browser really
runs, the login really succeeds, the assertion really evaluates against real page
content, and it really fails with a real expected-vs-observed mismatch. What's
"intentional" is only the choice of expected value, exactly like a regression test
deliberately written to fail until a fix lands — not a fabrication of the CLI's own
output. Alternative considered: assert against a selector that doesn't exist (a
`no-such-element` failure) — rejected because that failure mode doesn't demonstrate an
*assertion* mismatch (the case's most illustrative failure shape) and produces a less
informative screenshot (nothing highlighted, just the page as-is).

**New case file, not editing the existing passing one.** Add
`examples/cases-ui-journey/cases/example-ui-journey-demo-failure.yaml` (new file,
same directory, same fixture reused) rather than modifying
`example-ui-journey.yaml` — the existing case is real end-to-end proof this repo's own
example suite passes; turning it into an intentional failure would break that meaning.
The new file's id is `UI-JOURNEY-DEMO-FAILURE-1`, unambiguously named so it reads as
"demo of a failure" rather than a flaky test to fix.

**Running it stays a manual, documented step — not wired into this repo's own CI.**
This repo's `pr-annotations.yml` workflow runs `examples/` cases as its own dogfooding
gate; a case designed to always fail cannot be added to that gate without either being
skipped (defeating dogfooding) or breaking every PR. So: the new case lives in
`examples/cases-ui-journey/cases/` for discoverability and documentation
(`examples/cases-ui-journey/README.md` gets a note explaining it's excluded from the
repo's own CI gate on purpose), and is run once, manually, to produce the screenshot
committed to `docs/assets/ci/`, exactly like the other 5 screenshots in that directory.

**Command to produce the evidence:**
```bash
RELEASETWIN_UI_ENABLED=1 RELEASETWIN_EVIDENCE=on \
  RELEASETWIN_EVIDENCE_DIR=/tmp/releasetwin-demo-evidence \
  dotnet run --project src/ReleaseTwin.Cli -- \
  examples/cases-ui-journey/cases/example-ui-journey-demo-failure.yaml
```
(Or point at a directory containing only that one file — `RunAsync` takes a cases
directory; the exact invocation is confirmed in tasks.md against the CLI's actual
directory-vs-file argument handling.)

**docs/ci.md placement: a new short subsection right after the existing "PR
annotations" section**, titled something like "What a failure looks like" — pairing the
real `FAIL UI-JOURNEY-DEMO-FAILURE-1 (assertion): expected "Welcome back, valid user!",
observed "You logged into a secure area!"` line with the screenshot
(`docs/assets/ci/ui-failure-evidence.png`) and one sentence noting the evidence was
captured locally with no hosted account (`RELEASETWIN_EVIDENCE_DIR`, linking to the
Credentials section's existing bullet from `local-evidence-artifacts`). Exact prose
finalized during implementation.

## Risks / Trade-offs

- **`the-internet.herokuapp.com`'s exact flash-message copy could change** → same risk
  profile as the existing passing UI-journey case already accepts; if it changes, both
  cases need a one-time update, not just this one.
- **A case designed to always fail, sitting in `examples/`, could confuse a future
  contributor into thinking it's broken** → mitigated by the case id
  (`UI-JOURNEY-DEMO-FAILURE-1`), an inline case-file comment, and the README note called
  out above.
