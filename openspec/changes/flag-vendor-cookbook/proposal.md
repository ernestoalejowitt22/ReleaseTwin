## Why

`http-flag-control` already lets a flag-proof case toggle **any** HTTP-reachable
feature-flag system as case-file data — no adapter code required (see
`docs/flag-proof.md`'s `control` block). But the only worked, runnable examples
today are LaunchDarkly (a dedicated adapter) and one generic Entra-gated recipe
(`examples/cases/enterprise/example-flag-proof-http-entra.yaml`). Nobody
evaluating ReleaseTwin who happens to run Unleash or GrowthBook — both
open-source, self-hostable, and popular with exactly the dev-tool-native
audience the OSS funnel already targets — has anything to point at showing the
mechanism works against their actual stack, even though it already does.

This project has a consistent standard for this kind of claim: `docs/ci.md`
says outright "these aren't just typed — proven live," backed by real CI
screenshots, and the LaunchDarkly `control`+`verify` path is exercised nightly
against LaunchDarkly's real API. A "cookbook" of vendor snippets copied from
each vendor's public API docs, never run against a real instance, would be the
first documentation in this repo that breaks that standard — and a wrong
snippet for a flag-toggle endpoint is a worse experience than no snippet at
all. Both target vendors are self-hostable via a single Docker container, so
each recipe can be verified against a real running instance before it ships,
the same way `bitbucket-custom-pipe`'s wrapper was verified locally against
Docker rather than only argued about.

## What Changes

- **Unleash was tried and dropped** — see design.md. Its Admin API has no
  body-based way to set a flag's enabled state, only two dedicated action
  endpoints (`.../on`, `.../off`), which the `control` block's fixed token
  vocabulary (`{{state}}`, `{{enabled}}`) cannot select between. Confirmed
  against Unleash's own live OpenAPI spec after standing up a real instance —
  not a guess. Supporting it would need an engine change, out of scope here.
- Add `examples/cases-flag-proof-growthbook/` — a runnable flag-proof case (+
  fixture, + a `docker/` compose file to bring up a real local GrowthBook
  instance) verified end to end: `dotnet run --project src/ReleaseTwin.Cli --
  examples/cases-flag-proof-growthbook` against the real container reports
  `FLAGPROOF FLAGPROOF-GROWTHBOOK-DEMO-1 (Passed)`.
- Add a "Vendor cookbook" section to `docs/flag-proof.md`, reframed around
  what actually matters most: a flag stored in **your own database**, flipped
  by your own internal endpoint, needs no vendor at all — and this repo
  already has a live proof of exactly that shape
  (`docs/express.md`'s `PUT /admin/flags/orders-v2`). The GrowthBook recipe
  follows for teams running that specific platform. A closing note explains
  why Unleash isn't listed, so the gap reads as a documented limitation, not
  an oversight.
- `README.md`'s examples section gains the GrowthBook directory, in the same
  prose style `examples/cases-express/` is already listed in (not a literal
  table — the original proposal's phrasing was imprecise).
- Explicitly deferred: Flagsmith, PostHog, ConfigCat, Split, and any other
  vendor — added later if a specific one is asked for, following the same
  verified-not-just-typed bar (and checked against this same on/off-style
  pitfall before committing to a recipe). No dedicated (non-HTTP,
  non-LaunchDarkly) adapter for any of these — the whole point is that
  `http-flag-control` already covers body-driven vendors generically; a
  dedicated adapter is a separate, much larger piece of work (see the
  `launchdarkly-adapter` vs `http-flag-control` split already documented) and
  isn't justified until a vendor's toggle semantics (percentage rollouts,
  targeting rules) actually need it.

## Capabilities

### New Capabilities
_None. This change adds runnable examples and documentation exercising the
existing `http-flag-control` and `flag-proof` behaviour without changing any
requirement._

### Modified Capabilities
_None._

## Impact

- New directory: `examples/cases-flag-proof-growthbook/`.
- Extended doc: `docs/flag-proof.md`'s "Vendor cookbook" section; `README.md`'s
  examples section.
- No changes to `src/` or `tests/` — the engine is unchanged; this exercises
  `http-flag-control`, which already works.
- No manual/user steps — verification is done locally against each vendor's
  self-hosted Docker image before merging (Docker available in this session,
  already used for the Bitbucket Pipe work). GrowthBook's one-time account
  setup (no headless seed, unlike Unleash) was done once, locally, to obtain
  an API key for verification — not something a user of the example repeats;
  the example's own README documents that same one-time step for anyone
  running it themselves.
- `skip_specs: true` in `.openspec.yaml` (examples + docs; no spec-level
  behaviour change), matching the precedent set by
  `archive/2026-09-03-express-flag-proof-example`.
