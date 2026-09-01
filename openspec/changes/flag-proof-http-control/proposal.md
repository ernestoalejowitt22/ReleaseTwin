## Why

Flag proof — paired known-bad/known-good execution reported as one discriminating
verdict — is the product's most differentiated capability. But it only works if
the target feature flag lives in **Azure DevOps variable groups** or
**LaunchDarkly**: those are the only two `IFeatureStateController` implementations.

A prospect whose flag is toggled by any other means — a config service, their own
admin API, Unleash / Split / Flagsmith / ConfigCat's REST APIs, a Consul or etcd
KV, or an environment variable their CI controls — gets `Ineligible`: neither leg
runs, and the pitch collapses to "we can test your REST API," which is real but
not the thing with no competitor.

The generic HTTP adapter already tests *any* REST API from case-file data alone.
The same idea applied to flipping a flag closes the gap for most prospects: the
customer describes, in case data, the one HTTP request that sets the flag state,
and flag proof works — no adapter code per flag system.

## What Changes

- **New `http-flag-control` capability.** A `flag_proof` case can declare an HTTP
  `control` request. Before each leg, the runner performs that request with
  `{{featureKey}}` and `{{state}}` substituted (and the HTTP adapter's existing
  `${ENV_VAR}` interpolation for URLs / headers / bodies / credentials), setting
  the flag known-bad then known-good. The HTTP adapter — always installed, no
  credentials to install — provides this `IFeatureStateController`.
- **Flag polarity.** `control.known_bad_when: disabled` (default) or `enabled`, so
  a flag whose *off* state is the buggy one and a flag whose *on* state is the
  buggy one are both expressible.
- **Eligibility.** A `flag_proof` case that declares a `control` block is
  eligible for flag proof via the HTTP adapter; one that declares neither a
  `control` nor an adapter-provided controller stays `Ineligible` exactly as
  today.
- **Credentials stay in the customer's infra.** The `control` request resolves
  its auth from `${ENV_VAR}` or the hosted `project-secrets` capability, never
  from the case file — the same contract the HTTP adapter already enforces.

## Capabilities

### New Capabilities

- `http-flag-control`: a flag-proof case can set its target feature's state over
  a single, config-declared HTTP request, letting flag proof run against any
  flag system with a REST toggle — no per-system adapter code.

### Modified Capabilities

- `flag-proof`: the feature-state eligibility check is satisfied when a case
  declares an HTTP `control` block, not only when an installed adapter provides a
  feature-state controller.

## Impact

- **`ReleaseTwin.Adapters.Http`:** a new `HttpFeatureStateController` +
  `IFeatureStateControllerSource` on `HttpAdapter`; reuses the existing request
  builder / `${ENV}` interpolation / capture-free execution path.
- **`ReleaseTwin.Cli` case loading:** parse the optional `flag_proof.control`
  block (`method`, `url`, `headers`, `body`, `known_bad_when`); validate it the
  same way `http.request` parameters are validated.
- **`ReleaseTwin.Core`:** one new `FlagProofOutcome.ControlFailed` value and a
  `try/catch` around `SetStateAsync` in `FlagProofRunner.RunAsync`, so a rejected
  control request is a distinct *failing* outcome (not weak, not ineligible).
  `IFeatureStateController` and the paired-leg logic are otherwise unchanged.
- **docs:** `docs/quickstart.md` / a flag-proof section — "flag proof against your
  own flag system"; `examples/cases/` — an HTTP flag-proof example.
- **no hosted change** — flag-proof control is entirely CLI-side, in the
  customer's own infra.

## Explicitly deferred

- A `releasetwin.yaml`-level control template (define the endpoint once for many
  flag-proof cases). MVP is per-case; add project-level config if a design
  partner has enough flag-proof cases to want the DRY form.
- A response assertion on the control request (confirm the flag actually
  changed). The known-bad/known-good legs already prove the state took effect
  functionally; a direct read-back is a nicety.
- Non-HTTP toggles (a CLI command, a file write) — out of scope; those want
  their own small controller.
