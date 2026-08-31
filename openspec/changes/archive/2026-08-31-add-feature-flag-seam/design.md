## Context

See proposal.md — Why. Relevant current state:

- **Three runtimes**: `web/` (Next.js 16 / React 19, Vercel, RSC + client components,
  Clerk auth), `hosted/` (.NET minimal API on AWS Lambda, DynamoDB single-table,
  Terraform via CI/OIDC), and the CLI/engine (`src/*`, .NET, AGPL-3.0, runs inside
  *customers'* CI — including self-hosters — as a short-lived process).
- **Entitlements already exist**: `EntitlementService`, `PlanCatalog`, `plans.json`,
  and the `plan-catalog` / `plan-tier-gating` specs. This design must not blur into
  them.
- **No feature-flag SDK anywhere.** `ReleaseTwin.Adapters.LaunchDarkly` is a product
  adapter (tests customers' flags) and is unrelated.
- Constraint (CLAUDE.md): prefer code-side automation over standing manual config;
  CI-only deploys; single-table DynamoDB; scope proposal before implementation.

## Goals / Non-Goals

**Goals:**
- One flag-evaluation API surface per runtime, backed by OpenFeature, so switching to
  LaunchDarkly later is a provider registration change and nothing else.
- A single registry (`flags.json`) that is the source of truth for flag keys, types,
  and defaults, with drift caught in CI.
- Identical evaluation-context semantics across runtimes.
- Fail-open behaviour: a provider error or missing flag always yields the coded default,
  never an exception that breaks a request or a CLI run.
- Zero cost and zero new infra in Phase 1.

**Non-Goals (design-level):**
- Streaming/real-time flag updates. Phase 1 providers are static; a value change needs a
  redeploy (`web/`, `hosted/`) or a new CLI release / yaml edit.
- Percentage rollouts, targeting rules, A/B experiments — these are provider features
  that arrive with LaunchDarkly; the seam only has to *carry the context* they need.
- A management UI or audit log for flag changes.

## Decisions

### D1: OpenFeature as the abstraction, not a hand-rolled interface

OpenFeature (CNCF, Apache-2.0) is a vendor-neutral flag API with a provider SPI.
Application code calls `getBooleanValue(key, default, context)` against a global client;
the provider is registered once at startup.

- **Why**: the "prep now, adopt vendor later" goal is exactly its purpose. LaunchDarkly
  publishes official OpenFeature providers for both JS (`@launchdarkly/openfeature-server-provider`)
  and .NET (`LaunchDarkly.OpenFeature.ServerProvider`). Adoption = add package + swap the
  `setProvider` call.
- **Alternative rejected**: our own `IFlagProvider` interface. It is a reimplementation
  of OpenFeature's provider SPI with less tooling, no ecosystem providers, and no
  standard context/hook model — more code for a worse seam.
- **Alternative rejected**: Vercel Flags SDK. Excellent Next.js fit and free, but
  `web/`-only; would leave `hosted/` and the CLI with a different model, defeating the
  "one seam" goal.
- **Implementation note (applied)**: on `.NET` (`hosted/`, CLI) the swap point is the
  DI-registered `OpenFeature.FeatureProvider`, and `IFlagService` calls its resolve
  methods directly rather than going through OpenFeature's global `Api` client. This
  keeps evaluation free of process-global mutable state, which `WebApplicationFactory`
  tests run in parallel would otherwise race on. `web/` server code uses the global
  `OpenFeature` provider; `web/` client code uses a domain-scoped provider. Adopting
  LaunchDarkly is still a one-line registration change per surface.

### D2: `flags.json` at repo root is the single registry; parity enforced by test

Shape per entry: `key`, `type` (`boolean|string|number|object`), `default`,
`description`, `surfaces` (`["web","hosted","cli"]`), `owner`.

- `web/` loads it directly to seed the in-memory provider.
- `hosted/` and the CLI embed it as a compiled resource (copied at build, or linked
  file) to seed their providers and to define default constants.
- A test on each side asserts every key it references exists in `flags.json` with a
  matching type, and CI runs a small script asserting the JSON is well-formed and every
  `surfaces` entry is known. Fails the build on drift.
- **Alternative rejected**: codegen typed constants from `flags.json` into each language.
  More build machinery (a generator step in two toolchains) than a parity test, for
  marginal benefit at this flag count. Revisit if the registry grows past ~20 flags.
- **Alternative rejected**: no registry, parallel constants per surface. Guarantees
  drift.

### D3: Static providers per surface in Phase 1

| Surface | Provider | Value source | Flip a flag by |
|---|---|---|---|
| `web/` server + client | OpenFeature `InMemoryProvider` | `flags.json`, per-key override via `FLAG_<KEY>` env | redeploy / change Vercel env |
| `hosted/` | `InMemoryProvider` seeded from embedded `flags.json` | `appsettings.json` `FeatureFlags:` section + `FEATUREFLAGS__<KEY>` env override | redeploy |
| CLI / engine | `InMemoryProvider` seeded from embedded `flags.json` | compiled defaults + `featureFlags:` map in `releasetwin.yaml` | new release or yaml edit |

- **Lambda note**: the .NET OpenFeature client and `InMemoryProvider` are in-process and
  synchronous — no background thread, no streaming socket, safe across freeze/thaw. This
  is the specific reason we are *not* starting with the LaunchDarkly .NET SDK on
  `hosted/` (its streaming/daemon model fights Lambda; that is a later problem to solve
  with polling mode or a Relay Proxy).
- **Alternative rejected for `hosted/`**: a DynamoDB config item read per request so
  flags flip without a deploy. It is the right "2am kill switch" answer and is called out
  as the first follow-up, but it is not needed to establish the seam and adds a
  single-table access pattern + caching concern. Deferred deliberately.

### D4: One evaluation-context shape, built per surface

```
targetingKey : <organization id>          // stable bucketing key for future rollouts
attributes   : {
  userId     : <clerk user id | null>     // null for CLI / anonymous marketing
  plan       : <plan id | "unknown">
  projectId  : <project id | null>
  surface    : "web" | "hosted" | "cli"
  env        : "production" | "preview" | "development"
}
```

- `web/`: built from Clerk `auth()` (org + user) in RSC and from the Clerk client hook in
  components; `plan` from the same source the dashboard already uses. Marketing/anon
  pages pass a context with only `surface`/`env`.
- `hosted/`: built from the authenticated principal on each request (org, plan, project
  from the route).
- CLI: `organization`/`project` from `releasetwin.yaml` + the project API identity;
  `userId` null.
- **Why a fixed shape now**: LaunchDarkly targeting rules are written against context
  attribute names. Fixing them here means rules authored later "just work" and we never
  have to migrate a context schema across three codebases.

### D5: Feature flags are separate from plan entitlements

- Entitlements answer "does this plan *allow* X" and are enforced by `EntitlementService`
  against `plans.json` / Polar. Permanent.
- Flags answer "is code path X *switched on* for this context". Operational, temporary,
  deleted after GA.
- A flag MAY read `plan` from context (e.g. to dark-launch a feature to Pro orgs first)
  but MUST NOT be the mechanism that denies a plan-gated feature. The spec states this
  as a requirement so the boundary does not rot.

### D6: Flag naming

`kebab-case`, `<area>-<feature>[-vN]`, e.g. `dashboard-release-rollup`,
`billing-webhook-processing`, `flag-seam-smoke`. Boolean flags read as "on = feature
present". Documented in `docs/feature-flags.md`.

## Risks / Trade-offs

- **[Registry embedded in three places can still drift at build time]** → parity tests on
  each surface + a CI JSON-lint step; the test references the *same* file, linked not
  copied where the toolchain allows.
- **[OpenFeature .NET added to `ReleaseTwin.Core` increases the AGPL engine's dependency
  surface]** → it is Apache-2.0, single package, no heavy transitive deps; the engine
  already carries adapter SDKs. Acceptable; noted for the licensing review.
- **[Static provider gives a false sense of "we have flags" — no runtime flip]** →
  documented plainly in `docs/feature-flags.md`: Phase 1 flips need a deploy; runtime
  control is explicitly the LaunchDarkly-adoption follow-up.
- **[Team reaches for flags where an entitlement belongs, or vice versa]** → D5 written
  as a spec requirement + a decision table in the docs.
- **[`web/` layout.tsx is script-fragile under Next 16 / React 19 (see existing comment)]**
  → provider init for the client SDK is a normal client component/provider, not an
  inline `<script>`; server SDK init is module-level in a server-only file. No
  `beforeInteractive` script involved.

## Migration Plan

Phase 1 is additive — no behaviour changes, nothing to roll back beyond reverting the
PR. The smoke flag defaults on and gates nothing user-visible.

**When LaunchDarkly is later adopted** (separate change): add one provider package per
surface; register it at startup only when its SDK key env var is present, else keep the
static provider; load keys from AWS Secrets Manager (`hosted/`, CLI-via-endpoint) and
Vercel env (`web/`); author targeting rules against the D4 context. No application call
sites change.

## Open Questions

- Exact `appsettings` section name / env override delimiter for `hosted/` — cosmetic,
  settled during implementation.
- Whether the CLI should warn when `releasetwin.yaml` sets a flag key absent from the
  embedded registry, or ignore it silently — decide during implementation; does not
  affect specs or task breakdown.
