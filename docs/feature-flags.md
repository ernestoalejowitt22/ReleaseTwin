# Feature flags

ReleaseTwin gates its own functionality (not customers' — see the note below) through a
**vendor-neutral feature-flag seam** built on [OpenFeature](https://openfeature.dev). The
CLI/engine resolves flags from local sources only — no account, no network, no cost.
Adopting a real provider (LaunchDarkly or anything else) later is a one-file change; no
call site moves.

> Not to be confused with `src/ReleaseTwin.Adapters.LaunchDarkly` — that adapter tests
> *customers'* LaunchDarkly flags and is unrelated to this.

This repo covers the CLI/engine surface only. The hosted dashboard (`web/` and `hosted/`,
private repo `releasetwin-platform`) keeps its **own separate registry** of hosted-surface
flags, following the same pattern documented here; it is not covered by this doc.

## The registry: `flags.json`

One file at this repo's root is the source of truth for the engine/CLI's own flags:

```jsonc
{
  "flags": [
    {
      "key": "flag-seam-smoke",          // kebab-case, <area>-<feature>[-vN]
      "type": "boolean",                  // boolean | string | number | object
      "default": true,                    // must match `type`
      "description": "…",                 // what it gates, and the safe state
      "surfaces": ["cli"],
      "owner": "platform"
    }
  ]
}
```

- The CLI embeds it as a compiled resource.
- A malformed registry fails the CLI build's `FlagRegistryTests`.

### Adding a flag

1. Add an entry to `flags.json`.
2. Read it where you need it (see "Reading a flag" below). Reference the key through the
   generated/typed accessor so a typo is a compile error.
3. Ship. The flag resolves to its `default` everywhere until you override it.

### Naming

`kebab-case`, `<area>-<feature>[-vN]` — e.g. `dashboard-release-rollup`,
`billing-webhook-processing`. Boolean flags read as **on = feature present**.

## Reading a flag

CLI / engine: inject `IFlagService` from `ReleaseTwin.Core`,
`await flags.GetBooleanAsync("flag-seam-smoke", ctx)`.

The accessor **fails open**: a provider error, a missing key, or a wrong-typed value
returns the caller's coded default. Flag evaluation never throws.

(The hosted dashboard's `web/`/`hosted/` surfaces have their own accessors in
`releasetwin-platform` — `getFlag`/`useFlag` and an injected `IFlagService` respectively —
following the same fail-open contract. See that repo's own copy of this doc.)

## Flipping a flag today (local-only, Phase 1)

CLI: `featureFlags:` map in `releasetwin.yaml` (`featureFlags: { "flag-seam-smoke": false }`),
takes effect next run.

There is **no runtime flip without a deploy** in Phase 1. That is the first thing a real
provider buys you.

## Evaluation context

Every surface (including the hosted ones in `releasetwin-platform`) builds the same shape
so targeting rules authored later work unchanged:

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

For the CLI, in a separate change:

1. Add the provider package (`LaunchDarkly.OpenFeature.ServerProvider`) — via a hosted
   `GET /flags` endpoint, not a direct SDK.
2. At startup, register that provider **only when its SDK key env var is present**,
   otherwise keep the static provider. One registration point in the CLI composition root.
3. Author targeting rules against the evaluation-context attributes above.

No code that calls `IFlagService` changes.

(`web/`/`hosted/` follow the equivalent sequence against their own provider packages and
config sources — see the doc in `releasetwin-platform`.)
