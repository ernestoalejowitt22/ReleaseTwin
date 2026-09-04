## Why

`examples/cases-express` has exactly 2 cases (`EXPRESS-CONTRACT-1`,
`EXPRESS-FLAGPROOF-1`), both against the same single endpoint
(`GET /orders/:id`) of the demo app in the sibling `releasetwin-ci-examples`
repo. That app's own limited surface is the actual ceiling on how rich these
cases can get — `docs/express.md`'s full walkthrough, and every CI-platform
screenshot in `docs/ci.md` that runs this directory, currently show the same
two cases and the same `2 passed, 0 failed` line. Making the case set richer
requires giving the demo app a little more surface to test, not just writing
more YAML against what's already there.

## What Changes

**Correction from initial research**: the originally-sketched plan (a free
404 contract case, plus a validation flag that rejects with `400`) turned out
to be inexpressible with the CLI's current HTTP adapter — `http.request`
unconditionally fails its own step on any non-2xx response
(`HttpRequestOperation.cs:122`), with no parameter to declare a non-2xx
response as the expected, passing outcome. That would make a "correct 404"
contract case always report `FAIL` (not what it demonstrates), and would
make a "reject with 400 when the flag is on" case's known-good leg fail at
the transport step before its assertion ever ran — breaking the fail/pass
discrimination flag-proof depends on. Rather than route around a real
adapter limitation with a contrived app design (or rig the demo to declare
a wrong-status case as a "pass"), both new cases below are redesigned to
stay entirely within 2xx responses, the same way the existing `orders-v2`
example already does. This is a real gap worth knowing about, but fixing
the HTTP adapter to support an expected-status assertion is out of scope
for this docs/examples-only change — flagged for a possible separate future
change, not fixed here.

- **`releasetwin-ci-examples/apps/express-demo/server.js`** (separate repo):
  add one more small, realistic flagged behavior — a `POST /orders` endpoint
  gated by a new `currency-normalization` flag. A request with a
  lowercase/mixed-case currency code (e.g. `"usd"`) is stored and returned
  as-is when the flag is disabled (a real, relatable normalization bug), and
  upper-cased when enabled. Both legs return `201` — the difference is only
  in the response body, exactly mirroring how the existing `orders-v2` flag
  changes `$.taxed` without ever touching the status code.
- **`examples/cases-express/`** (this repo): two new case files —
  - `flag-state.yaml` (`EXPRESS-CONTRACT-FLAGSTATE-1`): a pure HTTP contract
    case against the app's existing `GET /admin/flags/orders-v2` introspection
    endpoint (already implemented, no app change) — asserts the flag's key
    and its default boot-time state, exercising a genuinely different part
    of the app's surface (the admin/flag API) than the existing two cases.
  - `currency-flag-proof.yaml` (`EXPRESS-FLAGPROOF-CURRENCY-1`): a
    flag-proof case toggling `currency-normalization`, reusing the shared
    control template in `releasetwin.yml` unchanged (it's already
    parametrized by `feature_key`).
- **`docs/express.md`**: update the walkthrough to describe all 4 cases and
  the real, re-run output (`4 passed, 0 failed`), including a short mention of
  the second flagged behavior alongside the existing tax example.
- No changes expected to any CI workflow file (GitHub Actions, Bitbucket
  Pipelines, or Azure Pipelines, in either repo) — all three already invoke
  the CLI against the whole `examples/cases-express` directory, so new case
  files there are picked up automatically. Verified during implementation,
  not assumed.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — this only adds example cases, a demo app endpoint, and docs; no
ReleaseTwin engine or adapter behavior changes.)

This change sets `skip_specs: true`.

## Impact

- **Cross-repo**: `server.js` lives in the separate public `releasetwin-ci-examples`
  repo (sibling checkout at `/Users/ernestoalejo/Projects/releasetwin-ci-examples`,
  currently clean on `main`). Edits there are made directly on disk during
  implementation; nothing is committed or pushed in either repo without being
  asked separately, per this repo's own git conventions.
- **This repo**: `examples/cases-express/` (2 new files), `docs/express.md`
  (walkthrough update). No `src/` changes.
- **No breaking changes** — the existing 2 cases and their behavior are
  untouched; this only adds a new endpoint/flag and two new cases alongside
  them.
