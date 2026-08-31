## Why

ReleaseTwin ships large features in rapid succession (dynamic plans, billing, evidence
viewer, trend analytics, CI PR integration) with no way to decouple *deploy* from
*release*: every merge is live for everyone the moment it lands, and there is no kill
switch for a risky subsystem. We want that capability in place — the API seam, the
evaluation context, the flag registry — before we need it, with a zero-cost static
provider today and a clean one-file swap to LaunchDarkly (or any provider) later.
`LaunchDarkly` already exists in this repo, but only as a *product adapter* that tests
customers' flags; nothing gates ReleaseTwin's own behavior.

## What Changes

- Introduce a **vendor-neutral feature-flag capability** built on **OpenFeature**
  (`@openfeature/server-sdk` + `@openfeature/web-sdk` in `web/`, `OpenFeature` NuGet in
  `hosted/` and the CLI/engine).
- Add a single **flag registry** (`flags.json` at repo root): every flag's key, type,
  default value, description, and owning surface. A CI parity check fails if code and
  registry drift.
- Wire a **static default provider** on all three surfaces — no network, no account, no
  cost:
  - `web/`: in-memory provider seeded from `flags.json`, overridable by env var.
  - `hosted/`: config-backed provider (`appsettings` + env), evaluated statically
    (Lambda-safe, no streaming).
  - CLI/engine: compiled-in defaults plus optional `releasetwin.yaml` overrides; fully
    offline / air-gap safe, works for self-hosters.
- Define one **evaluation context** shape shared by all surfaces (`targetingKey` = org
  id, plus user id, plan, project id, surface) so LaunchDarkly targeting rules work
  unchanged when a real provider is plugged in.
- Add typed flag accessors: `flags.ts` in `web/`, `IFlagService` in `hosted/` and the
  CLI.
- Ship **one inert smoke flag** (`flag-seam-smoke`, default on) read on each surface to
  prove the wiring end to end; no shipped feature is placed behind a flag in this change.
- Documentation: how to add a flag, how to flip it today, and exactly which file changes
  when LaunchDarkly is adopted.

Non-goals / explicitly deferred:
- Any LaunchDarkly SDK dependency, account, project, environment, SDK keys, or Relay
  Proxy. Nothing in this change touches LaunchDarkly.
- Runtime flag flips without redeploy on `hosted/` (a DynamoDB config item) — noted as a
  follow-up.
- A `GET /flags` endpoint for the CLI to fetch org-resolved flags from `hosted/` — the
  eventual LaunchDarkly integration point for the CLI, not needed for the seam.
- A dashboard UI showing flag state.
- Putting existing features (`evidence-viewer`, billing, etc.) behind flags.

## Capabilities

### New Capabilities
- `feature-flags`: evaluating vendor-neutral feature flags across the web app, hosted
  API, and CLI/engine from a shared registry and a shared evaluation-context shape,
  with a static provider by default and a documented provider-swap path. Covers the
  flag registry + parity check, the fail-open/fail-safe evaluation contract, the
  evaluation-context contract, and the separation from plan entitlements.

### Modified Capabilities
- None. Plan entitlements (`plan-catalog`, `plan-tier-gating`) are deliberately left
  unchanged; feature flags are a separate, operational gate and never enforce plan
  access.

## Impact

- **New dependencies**: OpenFeature JS SDKs in `web/package.json`; `OpenFeature` NuGet in
  `ReleaseTwin.Hosted.Api`, `ReleaseTwin.Cli`, and `ReleaseTwin.Core` (Apache-2.0,
  lightweight). No LaunchDarkly packages.
- **New files**: `flags.json` (repo root); `web/src/lib/flags.ts`; a `Flags`
  project/folder under `hosted/` and the CLI; `docs/feature-flags.md`.
- **Touched**: `web/src/app/layout.tsx` (provider init), `hosted/.../Program.cs` (DI
  registration), CLI startup, `releasetwin.yaml` schema + `ReleaseTwinConfig`, CI
  workflow (parity check step).
- **No infra change**: no Terraform, no new secrets, no Vercel env required for Phase 1.
- **Later, when LaunchDarkly is adopted** (out of scope here): add one provider package
  per surface, register it at startup behind an env check, and load SDK keys from AWS
  Secrets Manager / Vercel env.
