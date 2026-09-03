> **Scope reduced 2026-09-03.** The runnable `examples/express-demo/` app and the
> `express-example` CI job moved to the `ci-portability-live-examples` change
> (a `releasetwin-ci-examples` repo). This change keeps `examples/cases-express/`
> (the reference case YAML) and `docs/express.md`.

## Why

The engine's `http` adapter tests any REST API from case-file data alone, and
flag proof is the product's differentiator — but every bundled example points at
a public test API or Azure DevOps. A developer evaluating ReleaseTwin for their
own Node/Express service has nothing to clone that shows the known-bad → known-good
flip working end to end on a stack they recognise. The OSS funnel needs a
self-contained, runnable "release-proof your Express API" story with an SEO
surface, not just a YAML file that hits `jsonplaceholder`.

## What Changes

- Add `examples/express-demo/` — a minimal (~40-line) Express app with one real
  behaviour bug gated behind one feature flag, plus a local flag-toggle endpoint
  the `flag_proof.control` block can drive. Apache-2.0, self-contained, no
  database.
- Add `examples/cases-express/` — a flag-proof case (and fixture + `releasetwin.yml`
  with the shared `control` template) that runs known-bad/known-good against the
  demo app on `localhost`, plus a plain `http.assertJsonPath` case.
- Add `docs/express.md` — "Release-proof your Express API": run the demo, read the
  case, point it at your own service. Linked from `README.md` and `docs/quickstart.md`.
- Add an opt-in CI job (or fold into the nightly) that boots the demo app and runs
  the Express cases, so the example cannot rot silently. Node toolchain is
  introduced only for this job.
- `README.md` "What's here" / examples table updated to list the Express example.

## Capabilities

### New Capabilities
_None. This change adds runnable examples, documentation, and a CI job. It
exercises existing `http-adapter` and `flag-proof` behaviour without changing any
requirement._

### Modified Capabilities
_None._

## Impact

- New directories: `examples/express-demo/`, `examples/cases-express/`.
- New file: `docs/express.md`; edits to `README.md`, `docs/quickstart.md`.
- New CI job in `.github/workflows/` — first Node/npm toolchain in the repo
  (needs `gh auth` `workflow` scope to push). Runs `node`/`npm` to boot the demo,
  then the existing CLI against the Express cases.
- No changes to `src/` or `tests/` — the engine is unchanged.
- `skip_specs: true` in `.openspec.yaml` (examples + docs + tooling; no
  spec-level behaviour change).
