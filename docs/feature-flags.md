# Feature flags

ReleaseTwin gates its own functionality through a **vendor-neutral feature-flag seam**
built on [OpenFeature](https://openfeature.dev). Today every surface resolves flags from
local sources only — no account, no network, no cost. Adopting a real provider
(LaunchDarkly or anything else) later is a one-file change per surface; no call site
moves.

> Not to be confused with `src/ReleaseTwin.Adapters.LaunchDarkly` — that adapter tests
> *customers'* LaunchDarkly flags and is unrelated to this.

## The registry: `flags.json`

One file at the repo root is the source of truth for every flag:

```jsonc
{
  "flags": [
    {
      "key": "flag-seam-smoke",          // kebab-case, <area>-<feature>[-vN]
      "type": "boolean",                  // boolean | string | number | object
      "default": true,                    // must match `type`
      "description": "…",                 // what it gates, and the safe state
      "surfaces": ["web", "hosted", "cli"],
      "owner": "platform"
    }
  ]
}
```

- `web/` imports `flags.json` directly (same cross-root import as `hosted/plans.json`).
- `hosted/` and the CLI embed it as a compiled resource.
- A malformed registry fails `next build`, the hosted build's `FlagRegistryTests`, and
  the CLI build's `FlagRegistryTests`.

### Adding a flag

1. Add an entry to `flags.json`.
2. Read it where you need it (see per-surface APIs below). Reference the key through the
   generated/typed accessor so a typo is a compile error.
3. Ship. The flag resolves to its `default` everywhere until you override it.

### Naming

`kebab-case`, `<area>-<feature>[-vN]` — e.g. `dashboard-release-rollup`,
`billing-webhook-processing`. Boolean flags read as **on = feature present**.

## Reading a flag

| Surface | API |
|---|---|
| `web/` server (RSC, route handlers, actions) | `await getFlag("flag-seam-smoke", ctx)` from `@/lib/flags` |
| `web/` client components | `useFlag("flag-seam-smoke")` from `@/lib/flags-client` |
| `hosted/` | inject `IFlagService`, `await flags.GetBooleanAsync("flag-seam-smoke", ctx)` |
| CLI / engine | `IFlagService` from `ReleaseTwin.Core`, same API |

All accessors **fail open**: a provider error, a missing key, or a wrong-typed value
returns the caller's coded default. Flag evaluation never throws.

## Flipping a flag today (local-only, Phase 1)

| Surface | Mechanism | Takes effect |
|---|---|---|
| `web/` | env var `FLAG_<KEY_UPPER_SNAKE>` (e.g. `FLAG_FLAG_SEAM_SMOKE=false`), set in Vercel or `.env` | next deploy / restart |
| `hosted/` | config `FeatureFlags:<key>` in `appsettings*.json`, or env `FEATUREFLAGS__<key>` | next deploy |
| CLI | `featureFlags:` map in `releasetwin.yaml` (`featureFlags: { "flag-seam-smoke": false }`) | next run |

There is **no runtime flip without a deploy** in Phase 1. That is the first thing a real
provider buys you.

## Evaluation context

Every surface builds the same shape so targeting rules authored later work unchanged:

```
targetingKey : <organization id>   (absent for anonymous marketing / unconfigured CLI)
attributes:
  userId    : <clerk user id | absent>
  plan      : <plan id | "unknown">
  projectId : <project id | absent>
  surface   : "web" | "hosted" | "cli"
  env       : "production" | "preview" | "development"
```

## Feature flags vs. plan entitlements

They are **different gates** — do not blur them:

| | Entitlements (`EntitlementService` / `plans.json`) | Feature flags (`flags.json`) |
|---|---|---|
| Question | "Does this plan *allow* it?" | "Is this code path *switched on*?" |
| Lifetime | Permanent | Temporary — delete after GA |
| Driven by | Billing / plan | Rollout / ops / kill-switch |

A flag MAY read `plan` from context to stage a rollout. A flag MUST NOT be how a
plan-gated feature is denied, and enabling a flag never grants an entitlement.

## Adopting an external provider later

Per surface, in a separate change:

1. Add the provider package
   (`@launchdarkly/openfeature-server-provider` / `@launchdarkly/openfeature-web-browser` for
   `web/`; `LaunchDarkly.OpenFeature.ServerProvider` for `hosted/` and — via a hosted
   `GET /flags` endpoint, not a direct SDK — the CLI).
2. At startup, register that provider **only when its SDK key env var is present**,
   otherwise keep the static provider. `web/src/lib/flags.ts`, `hosted/Program.cs`, the
   CLI composition root — one registration point each.
3. Load SDK keys from AWS Secrets Manager (`hosted/`) / Vercel env (`web/`).
4. Author targeting rules against the evaluation-context attributes above.

No code that calls `getFlag` / `IFlagService` changes.
