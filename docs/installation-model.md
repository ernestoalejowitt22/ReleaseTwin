# Installation model

Cross-phase reference. Not scoped to any single change — every phase's design should stay compatible with this, even before a CLI or hosted control plane exists. Adapted from a prior internal suite's `docs/the initial design brief.md` (customer onboarding / deployment model sections) for ReleaseTwin's actual current state, not copied from it.

## Where things stand today

As of `hosted-self-serve-platform` (Stage 1), all three installation types below are real, not aspirational:

- **Local CLI**: `ReleaseTwin.Cli` (`dotnet run` from source, still not packaged — no npm/NuGet/Docker/GitHub Action) loads YAML case files, composes whichever adapters are configured (the generic HTTP adapter always; Azure DevOps only if its 5 env vars are present — resolved in `phase4-generic-http-adapter`, no longer hardcoded to one adapter), executes them, and exits non-zero on any failure.
- **CI runner**: the same CLI, scriptable into any CI pipeline — still the recommended default, per the initial design brief own reasoning (lowest infra cost, fewest security objections).
- **Hosted control plane**: two services as of `hosted-react-frontend` — `hosted/ReleaseTwin.Hosted.Api` (JSON-only .NET API: self-serve Clerk-backed signup, provider-neutral, not tied to a GitHub account; project/token management; ingest) and `web/` (Next.js/React/Tailwind/shadcn-ui, owning all UI, calling the API server-side only — a BFF, never exposing the API to the browser directly). Execution still happens entirely in the customer's own infra (CLI, local or CI); only report *metadata* is ever uploaded (case ID, oracle reference, fixture hash, pass/fail, classification — never fixture content, response bodies, or secrets). Stage 1 is **free-only** — no billing, no Stripe integration, no paid tiers exist yet (deliberately deferred to a future change once Stage 1 has real self-serve usage to monetize).

## Three installation types (all real now)

```
┌─────────────────┐   ┌──────────────────┐   ┌────────────────────┐
│   Local CLI      │   │   CI runner       │   │  Hosted control     │
│                  │   │                   │   │  plane               │
│ Runs on a        │   │ Runs inside the   │   │ Self-serve signup,   │
│ developer's      │   │ customer's own    │   │ dashboard, run       │
│ machine          │   │ CI pipeline       │   │ history — execution  │
│                  │   │                   │   │ still stays in the   │
│ Lowest friction   │   │ Recommended       │   │ customer's own infra │
│ for evaluation    │   │ default: no new   │   │ (this is NOT a       │
│                  │   │ infra to trust     │   │ hosted execution     │
│                  │   │                   │   │ runner)               │
└─────────────────┘   └──────────────────┘   └────────────────────┘
```

Per the initial design brief doc, **CI runner is the right first default** — lowest infrastructure cost, fewest security objections, no new service to trust with credentials. The hosted control plane doesn't change that: it's an optional dashboard layered on top, not a hosted execution runner. Hosted/private *execution* runners (a fundamentally different thing — your infra runs the customer's tests) are explicitly still not built, and shouldn't be until there's demand for scheduling or managed compute the customer can't run themselves.

## Onboarding difficulty tiers (targets, not measured)

- **Smoke check** (~5-15 min): can we reach a target and get a basic result. The HTTP adapter example needs zero credentials, so this is achievable today with no setup at all beyond cloning and building.
- **Existing test suite connected** (~20-60 min): wiring an existing Playwright/test project in, once that connector exists (still not built).
- **Real release proof** (1-2 engineering days): APIs, fixtures, vendors, feature flags all wired for real. This is the differentiated product, not something that should be sold as "five-minute setup."

## What this constrains, confirmed in practice

1. **No hardcoded credentials in adapter code.** Confirmed across every adapter built so far (Azure DevOps, generic HTTP) and now the hosted platform itself: Clerk Client ID/Secret and the database connection string are all configuration, never literals.
2. **No adapter assumes a specific host process.** Confirmed: the same adapters work identically from unit tests, the CLI, and (for the HTTP adapter specifically) now also drive real uploads to the hosted ingest API.
3. **Uploaded data never includes anything sensitive.** New with the hosted platform, but not a new design decision — it falls directly out of `CaseReport`/`FlagProofResult`'s existing metadata-only shape from Phase 1. The ingest API's contract has no field capable of carrying fixture content, response bodies, or credentials (tested explicitly).

## Default vs. opt-in functionality

Every capability beyond the execution kernel itself is opt-in, gated by whether its configuration is present — never a hard dependency the customer has to satisfy just to run the product. This isn't incidental: it's what "no adapter assumes a specific host process" and "no hardcoded credentials" already imply once you ask *what happens when a given adapter's config is simply absent*, and it matters commercially because no design partner has every vendor (LaunchDarkly, Azure DevOps, any specific host process) — assuming one would turn a smoke check into a blocked pilot.

| Capability | Default (zero-config) | Opt-in (config-gated) |
|---|---|---|
| Core execution kernel, CLI, generic HTTP adapter | ✓ always installed, no credentials needed | — |
| Azure DevOps adapter (work items, prerequisites, cleanup, variable-group flag proof) | — | ✓ installed only when all 5 `AZDO_*` env vars are set; a partial set is a startup error, not a silent skip (`cli-runner` spec) |
| Flag-proof execution in the CLI (`cli-flag-proof-runner`) | — | ✓ a case opts in per-file via its `flag_proof` block; if declared but no installed adapter exposes `IFeatureStateController`, the CLI reports that case `Ineligible` rather than failing the whole run |
| Hosted upload (ingest API, dashboard) | — | ✓ only if `RELEASETWIN_API_TOKEN` is set; upload failure is a warning, never a case failure |
| Project connections (`project-connections`) — labeling a project with an external GitHub repo | — | ✓ only if a separate `GitHubConnection:*` OAuth App is configured on the hosted API; display metadata only, no token is ever stored, no execution behavior |

The pattern that matters going forward: **any future flag-state backend is a new opt-in adapter, not a new default dependency.** Concretely, when a generic (non-Azure-DevOps) flag-proof mechanism gets built — LaunchDarkly, ConfigCat, Unleash, a customer's own config service, whatever a specific design partner actually uses — it should:

- Install only when its own credentials/config are present (same partial-config-is-an-error rule as Azure DevOps today), never assumed to exist.
- Leave the CLI and Core fully functional with zero flag-control adapters installed — exactly as `FlagProofRunner`'s existing capability check and the CLI's `Ineligible` outcome already guarantee for Azure DevOps's absence.
- Not be built speculatively for a vendor nobody has asked for; per `docs/customer-pilot-guide.md`, this is scoped to whatever a specific design partner's workflow actually requires, one adapter per real need, not a library of connectors built in advance.

## What stays deferred

- **Packaging/distribution** for the CLI: Docker image, GitHub Action, npm wrapper. Still `dotnet run` from source only.
- **Billing** (Stage 2 of `hosted-self-serve-platform`): Stripe integration, paid tiers, usage enforcement. Deliberately deferred until Stage 1 has real self-serve usage to monetize.
- **Config-driven adapter selection** in the CLI beyond the current two (HTTP unconditional, Azure DevOps conditional) — still code-level, not a config file naming arbitrary adapters.
- **A non-REST adapter** (message queue, database, vendor SDK without a REST surface) — the HTTP adapter covers anything with a REST surface; nothing else has needed a bespoke adapter yet.
- **A generic (non-Azure-DevOps) flag-proof mechanism**: the CLI now runs flag-proof pairs end-to-end for cases that declare a `flag_proof` block (`cli-flag-proof-runner`), but only against Azure DevOps's variable-group `IFeatureStateController` — a flag source that isn't Azure DevOps still needs a new implementation.
- **External-check connector (Playwright)** — visual/browser evidence isn't wired in as a *product* adapter (testing a customer's own UI). This is separate from `web/`'s own e2e coverage: `hosted-react-frontend` deliberately didn't add browser-level tests of ReleaseTwin's own frontend speculatively, but `web-cypress-e2e` added real Cypress coverage once that need was named directly — one spec automating a real sign-in (against a live Clerk instance, not mocked) through dashboard actions. Still local-only — no CI wiring for it yet, matching this project's total absence of a CI pipeline. Its own implementation (real, not simulated) is what actually caught a genuine JWT-claim-mapping bug in `hosted-react-frontend`'s auth setup that unit tests and manual verification both missed.
- **Enterprise features** (SSO, audit logs, custom retention, private/on-prem hosted deployment) — per the initial design brief, these come after paid demand is demonstrated, not before.
