## Context

See [proposal.md](proposal.md) — Why. The engine is unchanged by this work; the
`http` adapter and `flag_proof.control` block already do everything the example
needs. The design questions are all about the *example harness*: what the demo
target is, where it lives, and how CI keeps it honest without dragging a full
Node build into a .NET repo.

Constraints:
- `examples/` and `integrations/` are Apache-2.0; `src/`/`tests/` are AGPL. The
  demo app must live under `examples/` and carry an Apache-2.0 header.
- CLAUDE.md: "code-side automation over standing manual configuration" — the CI
  job must boot the demo itself, not depend on a deployed instance.
- The repo currently has no Node toolchain and no npm lockfile policy.
- Case files must never contain a literal credential; `${VAR}` only. The demo's
  local toggle endpoint should need no auth so the example runs with zero setup.

## Goals / Non-Goals

**Goals:**
- A `git clone` + two commands shows a flag-proof `Passed` verdict against a
  local Express app, with a visible known-bad → known-good flip.
- An SEO/landing surface (`docs/express.md`) that names Express, Fastify, Nest.
- CI proves the example still works on every run, with minimal added surface.

**Non-Goals:**
- A reusable Express *adapter* — the whole point is that none is needed.
- Testing the demo app itself (it is a fixture, not a product).
- A Node version matrix, published npm package, or Dependabot coverage for the
  demo's deps beyond a pinned lockfile.
- Covering Fastify/Nest with their own demo apps — one Express app; the docs note
  the others are the same `http` case.

## Decisions

### D1: Bundled Express app, not a public API or a separate repo
A `flag_proof` story needs a target whose flag we can actually toggle known-bad
then known-good. No public test API offers that. Options considered:
- **Public API + mock flag** — can't demonstrate a real behaviour change; reduces
  to the existing `jsonplaceholder` example. Rejected.
- **Separate `releasetwin-examples` repo** — keeps this repo .NET-only, but splits
  the funnel doc from the code it describes and adds a cross-repo CI trigger.
  Rejected for now; revisit if more demo apps accumulate.
- **Bundled `examples/express-demo/`** — chosen. ~40 lines, one route with a bug
  behind `process.env`-backed flag state, one `POST /admin/flags/:key` toggle
  endpoint (no auth, in-memory). Self-contained, versioned with its case.

### D2: Flag state is in-memory in the demo, toggled over HTTP
The `flag_proof.control` block drives `PUT`/`POST` to the demo's own toggle
endpoint per leg — exactly the "flag system with no adapter" path the HTTP
control block exists for. This makes the example *also* a live doc for
`control` + `verify`. The demo holds flag state in a module-level object; a
`GET /admin/flags/:key` backs an optional `control.verify` block in the case.

### D3: CI runs the example in the existing test workflow, gated by a path filter
Add a job to `ci.yml` (not a new workflow file) that runs only when
`examples/express-demo/**` or `examples/cases-express/**` changes, plus on the
nightly. Steps: `actions/setup-node`, `npm ci` in `examples/express-demo/`,
`node server.js &`, wait for `/healthz`, then run the already-built CLI against
`examples/cases-express/`. Uses the CLI from the repo build — no Docker pull.
Alternative (fold into nightly only) rejected: a funnel-critical example should
break the PR that breaks it.

### D4: `npm ci` needs a committed lockfile; deps kept to `express` only
`package-lock.json` is committed. The demo depends on `express` and nothing else
(no dotenv, no body-parser — Express 4.16+ has `express.json()`). Keeps the
supply-chain footprint a single well-known package.

### D5: Two cases, one `releasetwin.yml`
- `cases-express/flag-proof.yaml` — the headline: `flag_proof` with `feature_key`
  only, inheriting the shared `control` (+ `verify`) template from
  `cases-express/releasetwin.yml`.
- `cases-express/contract.yaml` — a plain `http.request` + `http.assertJsonPath`
  showing the no-flag path.
This mirrors `examples/cases-flag-proof-shared-control/` so a reader who saw that
example recognises the shape.

## Risks / Trade-offs

- **Node toolchain in a .NET repo** → confined to one CI job behind a path
  filter and to `examples/express-demo/`; the `dotnet build`/`dotnet test`
  contributor loop is untouched. Documented in `examples/express-demo/README.md`.
- **Demo app rots (Express major bump, Node EOL)** → pinned lockfile + CI job
  catches breakage; `engines` field pins a Node major.
- **Port collision on CI / local (`3000`)** → demo reads `PORT`, defaults to a
  less-common port (e.g. `4599`); the case's `${API_BASE_URL}` points at it.
- **Reader expects a packaged Express integration** → `docs/express.md` and the
  README table are explicit: "no adapter, no plugin — the `http` adapter reads
  your API from case data."
- **`control.verify` false-negative if in-memory state lags** → not a risk here;
  the demo is single-process read-your-writes. Noted in the case comment as the
  condition that makes `verify` safe.
